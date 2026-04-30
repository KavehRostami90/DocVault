using DocVault.Application.Common.Paging;
using DocVault.Application.UseCases.Search;
using DocVault.Domain.Documents;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pgvector;


namespace DocVault.Infrastructure.Persistence.Repositories;

/// <summary>
/// Chunk-level semantic search using pgvector cosine similarity on <c>DocumentChunks</c>.
/// Groups by document, picks the closest chunk per document, and converts distance to a score.
/// Activated when a query embedding is available, the database is PostgreSQL, and no text terms
/// are present (the <see cref="HybridSearchStrategy"/> is preferred when both vector and terms exist).
/// </summary>
internal sealed class PgvectorSearchStrategy : IDocumentSearchStrategy
{
  private const double DistanceThreshold = 0.7;

  public bool CanHandle(DocVaultDbContext db, float[]? queryVector, string[] terms) =>
    db.Database.IsRelational() && queryVector is not null && terms.Length == 0;

  public async Task<Page<SearchResultItem>> SearchAsync(
    DocVaultDbContext db,
    string[] terms,
    int page,
    int size,
    Guid? ownerId,
    float[]? queryVector,
    CancellationToken ct)
  {
    var ownerFilter = ownerId.HasValue ? "AND d.\"OwnerId\" = @ownerId" : string.Empty;

    // CTE: pick the single closest chunk per document, then join to Documents for owner filtering.
    var sql = $"""
        WITH chunk_distances AS (
            SELECT
                c."DocumentId",
                c."Text"                                              AS chunk_text,
                (c."Embedding" <=> @embedding)                        AS distance,
                ROW_NUMBER() OVER (
                    PARTITION BY c."DocumentId"
                    ORDER BY     c."Embedding" <=> @embedding
                )                                                     AS rn
            FROM "DocumentChunks" c
            WHERE c."Embedding" IS NOT NULL
        ),
        best_per_doc AS (
            SELECT "DocumentId", chunk_text, distance
            FROM   chunk_distances
            WHERE  rn = 1 AND distance < @threshold
        )
        SELECT b."DocumentId", b.chunk_text, b.distance, d."Title", d."FileName"
        FROM   best_per_doc b
        JOIN   "Documents" d ON d."Id" = b."DocumentId"
        WHERE  1=1 {ownerFilter}
        ORDER  BY b.distance
        LIMIT  @size OFFSET @offset
        """;

    await db.Database.OpenConnectionAsync(ct);
    try
    {
      var conn = (NpgsqlConnection)db.Database.GetDbConnection();

      var raw = new List<(DocumentId Id, string? ChunkText, double Distance, string Title, string FileName)>();

      using (var cmd = new NpgsqlCommand(sql, conn))
      {
        cmd.Parameters.AddWithValue("embedding", new Vector(queryVector!));
        cmd.Parameters.AddWithValue("threshold", DistanceThreshold);
        cmd.Parameters.AddWithValue("size",      size);
        cmd.Parameters.AddWithValue("offset",    (page - 1) * size);
        if (ownerId.HasValue)
          cmd.Parameters.AddWithValue("ownerId", ownerId.Value);

        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
          raw.Add((
            new DocumentId(reader.GetGuid(0)),
            reader.IsDBNull(1) ? null : reader.GetString(1),
            reader.GetDouble(2),
            reader.GetString(3),
            reader.GetString(4)));
      }

      var tags = await LoadTagsAsync(conn, raw.Select(r => r.Id.Value).ToArray(), ct);

      var items = raw
        .Select(r => new SearchResultItem(
            new DocumentSearchSummary(r.Id, r.Title, r.FileName,
                tags.GetValueOrDefault(r.Id.Value, [])),
            Math.Round(1.0 - r.Distance, 4),
            r.ChunkText))
        .ToList();

      return new Page<SearchResultItem>(items, page, size, items.Count);
    }
    finally
    {
      await db.Database.CloseConnectionAsync();
    }
  }

  private static async Task<Dictionary<Guid, IReadOnlyList<string>>> LoadTagsAsync(
    NpgsqlConnection conn, Guid[] docIds, CancellationToken ct)
  {
    var result = new Dictionary<Guid, IReadOnlyList<string>>();
    if (docIds.Length == 0) return result;

    using var cmd = new NpgsqlCommand(
      """
      SELECT dt."DocumentId", t."Name"
      FROM   "DocumentTag" dt
      JOIN   "Tags" t ON t."Id" = dt."TagsId"
      WHERE  dt."DocumentId" = ANY(@ids)
      """, conn);
    cmd.Parameters.AddWithValue("ids", docIds);

    using var reader = await cmd.ExecuteReaderAsync(ct);
    var temp = new Dictionary<Guid, List<string>>();
    while (await reader.ReadAsync(ct))
    {
      var id   = reader.GetGuid(0);
      var name = reader.GetString(1);
      if (!temp.TryGetValue(id, out var list))
        temp[id] = list = [];
      list.Add(name);
    }
    foreach (var kvp in temp) result[kvp.Key] = kvp.Value;
    return result;
  }
}
