using System.Text.RegularExpressions;
using DocVault.Application.Common.Paging;
using DocVault.Application.UseCases.Search;
using DocVault.Domain.Documents;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pgvector;

namespace DocVault.Infrastructure.Persistence.Repositories;

internal sealed partial class HybridSearchStrategy : IDocumentSearchStrategy
{
  private const double DistanceThreshold = 0.8;
  private const int    CandidateLimit    = 50;
  private const int    RrfK              = 60;
  private const int    AnnPrefetch       = CandidateLimit * 5;
  private const int    CommandTimeoutSec = 30;

  public bool CanHandle(DocVaultDbContext db, float[]? queryVector, string[] terms) =>
    db.Database.IsRelational() && queryVector is not null && terms.Length > 0;

  public async Task<Page<SearchResultItem>> SearchAsync(
    DocVaultDbContext db,
    string[] terms,
    int page,
    int size,
    Guid? ownerId,
    float[]? queryVector,
    CancellationToken ct)
  {
    var ownerFilter = ownerId.HasValue ? """AND d."OwnerId" = @ownerId""" : string.Empty;
    var tsQuery     = BuildTsQuery(terms);

    var ftsBlock = string.IsNullOrEmpty(tsQuery)
      ? """
        fts_candidates AS (SELECT NULL::uuid AS "DocumentId" WHERE FALSE),
        fts_ranked     AS (SELECT NULL::uuid AS "DocumentId", 0 AS rank WHERE FALSE),
        """
      : $"""
        fts_candidates AS (
            SELECT d."Id" AS "DocumentId",
                   ts_rank(d."SearchVector", to_tsquery('english', @tsQuery)) AS score
            FROM   "Documents" d
            WHERE  d."SearchVector" @@ to_tsquery('english', @tsQuery)
            {ownerFilter}
            ORDER  BY score DESC
            LIMIT  {CandidateLimit}
        ),
        fts_ranked AS (
            SELECT "DocumentId", ROW_NUMBER() OVER (ORDER BY score DESC) AS rank
            FROM   fts_candidates
        ),
        """;

    var sql = $"""
        WITH vec_candidates AS (
            SELECT
                c."DocumentId",
                c."Text"                              AS chunk_text,
                (c."Embedding" <=> @embedding)        AS distance
            FROM "DocumentChunks" c
            WHERE c."Embedding" IS NOT NULL
            ORDER BY c."Embedding" <=> @embedding
            LIMIT {AnnPrefetch}
        ),
        vec_best AS (
            SELECT DISTINCT ON (vc."DocumentId")
                vc."DocumentId", vc.chunk_text, vc.distance
            FROM   vec_candidates vc
            JOIN   "Documents" d ON d."Id" = vc."DocumentId"
            WHERE  vc.distance < @threshold
            {ownerFilter}
            ORDER  BY vc."DocumentId", vc.distance
        ),
        vec_ranked AS (
            SELECT "DocumentId", chunk_text,
                   ROW_NUMBER() OVER (ORDER BY distance) AS rank
            FROM   vec_best
            ORDER  BY distance
            LIMIT  {CandidateLimit}
        ),
        {ftsBlock}
        rrf AS (
            SELECT
                COALESCE(v."DocumentId", f."DocumentId")               AS doc_id,
                v.chunk_text,
                COALESCE(1.0 / ({RrfK}.0 + v.rank::float8), 0)
                + COALESCE(1.0 / ({RrfK}.0 + f.rank::float8), 0)      AS rrf_score
            FROM       vec_ranked v
            FULL OUTER JOIN fts_ranked f ON v."DocumentId" = f."DocumentId"
        )
        SELECT r.doc_id, r.chunk_text, r.rrf_score, d."Title", d."FileName"
        FROM   rrf r
        JOIN   "Documents" d ON d."Id" = r.doc_id
        ORDER  BY r.rrf_score DESC
        LIMIT  @size OFFSET @offset
        """;

    await db.Database.OpenConnectionAsync(ct);
    try
    {
      var conn = (NpgsqlConnection)db.Database.GetDbConnection();
      var raw  = new List<(DocumentId Id, string? ChunkText, double Score, string Title, string FileName)>();

      using (var cmd = new NpgsqlCommand(sql, conn) { CommandTimeout = CommandTimeoutSec })
      {
        cmd.Parameters.AddWithValue("embedding", new Vector(queryVector!));
        cmd.Parameters.AddWithValue("threshold", DistanceThreshold);
        cmd.Parameters.AddWithValue("size",      size);
        cmd.Parameters.AddWithValue("offset",    (page - 1) * size);
        if (!string.IsNullOrEmpty(tsQuery))
          cmd.Parameters.AddWithValue("tsQuery", tsQuery);
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

      var tags  = await LoadTagsAsync(conn, raw.Select(r => r.Id.Value).ToArray(), ct);
      var items = raw
        .Select(r => new SearchResultItem(
            new DocumentSearchSummary(r.Id, r.Title, r.FileName,
                tags.GetValueOrDefault(r.Id.Value, [])),
            Math.Round(r.Score, 6),
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
      """, conn) { CommandTimeout = CommandTimeoutSec };
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

  private static string BuildTsQuery(string[] terms)
  {
    var safe = terms
      .Select(t => SafeTerm().Replace(t, ""))
      .Where(t => !string.IsNullOrEmpty(t))
      .Select(t => t + ":*");
    return string.Join(" | ", safe);
  }

  [GeneratedRegex(@"[^a-zA-Z0-9À-ɏ\-_]")]
  private static partial Regex SafeTerm();
}
