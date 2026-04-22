using System.Globalization;
using DocVault.Application.Common.Paging;
using DocVault.Application.UseCases.Search;
using DocVault.Domain.Documents;
using Microsoft.EntityFrameworkCore;
using Npgsql;

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
    var vectorLiteral = ToVectorLiteral(queryVector!);
    var ownerFilter   = ownerId.HasValue ? "AND d.\"OwnerId\" = @ownerId" : string.Empty;

    // CTE: pick the single closest chunk per document, then join to Documents for owner filtering.
    var sql = $"""
        WITH chunk_distances AS (
            SELECT
                c."DocumentId",
                c."Text"                                              AS chunk_text,
                (c."Embedding" <=> {vectorLiteral})                   AS distance,
                ROW_NUMBER() OVER (
                    PARTITION BY c."DocumentId"
                    ORDER BY     c."Embedding" <=> {vectorLiteral}
                )                                                     AS rn
            FROM "DocumentChunks" c
            WHERE c."Embedding" IS NOT NULL
        ),
        best_per_doc AS (
            SELECT "DocumentId", chunk_text, distance
            FROM   chunk_distances
            WHERE  rn = 1 AND distance < @threshold
        )
        SELECT b."DocumentId", b.chunk_text, b.distance
        FROM   best_per_doc b
        JOIN   "Documents" d ON d."Id" = b."DocumentId"
        WHERE  1=1 {ownerFilter}
        ORDER  BY b.distance
        LIMIT  @size OFFSET @offset
        """;

    await db.Database.OpenConnectionAsync(ct);
    try
    {
      var conn    = (NpgsqlConnection)db.Database.GetDbConnection();
      var matches = new List<(DocumentId Id, double Distance, string ChunkText)>();

      using (var cmd = new NpgsqlCommand(sql, conn))
      {
        cmd.Parameters.AddWithValue("threshold", DistanceThreshold);
        cmd.Parameters.AddWithValue("size",      size);
        cmd.Parameters.AddWithValue("offset",    (page - 1) * size);
        if (ownerId.HasValue)
          cmd.Parameters.AddWithValue("ownerId", ownerId.Value);

        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
          matches.Add((new DocumentId(reader.GetGuid(0)), reader.GetDouble(2), reader.GetString(1)));
      }

      if (matches.Count == 0)
        return new Page<SearchResultItem>([], page, size, 0);

      var matchedIds  = matches.Select(m => m.Id).ToList();
      var docs        = await db.Documents
        .Include(d => d.Tags)
        .Where(d => matchedIds.Contains(d.Id))
        .ToListAsync(ct);

      var metaById = matches.ToDictionary(m => m.Id);
      var items = docs
        .OrderBy(d => metaById[d.Id].Distance)
        .Select(d => new SearchResultItem(
            d,
            Math.Round(1.0 - metaById[d.Id].Distance, 4),
            metaById[d.Id].ChunkText))
        .ToList();

      return new Page<SearchResultItem>(items, page, size, items.Count);
    }
    finally
    {
      await db.Database.CloseConnectionAsync();
    }
  }

  private static string ToVectorLiteral(float[] v)
  {
    var values = string.Join(",", v.Select(f => f.ToString("G9", CultureInfo.InvariantCulture)));
    return $"'[{values}]'::vector";
  }
}
