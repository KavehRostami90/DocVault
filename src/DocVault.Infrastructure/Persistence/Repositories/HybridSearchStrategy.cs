using System.Globalization;
using System.Text.RegularExpressions;
using DocVault.Application.Common.Paging;
using DocVault.Application.UseCases.Search;
using DocVault.Domain.Documents;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DocVault.Infrastructure.Persistence.Repositories;

/// <summary>
/// Hybrid search strategy that fuses chunk-level vector similarity and full-text search rankings
/// using Reciprocal Rank Fusion (RRF, K=60).
/// <para>
/// Top-50 vector candidates (from <c>DocumentChunks</c>, best chunk per document) and top-50
/// FTS candidates are merged: <c>rrf = 1/(60+vec_rank) + 1/(60+fts_rank)</c>. Documents that
/// appear in only one list still receive a partial score.
/// </para>
/// Activated when a query embedding is available, the database is PostgreSQL, and at least one
/// text term is present — i.e. it takes priority over <see cref="PgvectorSearchStrategy"/>.
/// </summary>
internal sealed partial class HybridSearchStrategy : IDocumentSearchStrategy
{
  private const double DistanceThreshold = 0.8; // slightly relaxed — FTS compensates for weak vector hits
  private const int    CandidateLimit    = 50;
  private const int    RrfK              = 60;

  public bool CanHandle(DocVaultDbContext db, float[]? queryVector) =>
    db.Database.IsRelational() && queryVector is not null;

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

    var tsQuery = BuildTsQuery(terms);

    // When there are no usable FTS terms, fall back to pure semantic ranking.
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
            ORDER  BY score DESC
            LIMIT  {CandidateLimit}
        ),
        fts_ranked AS (
            SELECT "DocumentId", ROW_NUMBER() OVER (ORDER BY score DESC) AS rank
            FROM   fts_candidates
        ),
        """;

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
        vec_best AS (
            SELECT "DocumentId", chunk_text, distance
            FROM   chunk_distances
            WHERE  rn = 1 AND distance < @threshold
            ORDER  BY distance
            LIMIT  {CandidateLimit}
        ),
        vec_ranked AS (
            SELECT "DocumentId", chunk_text, ROW_NUMBER() OVER (ORDER BY distance) AS rank
            FROM   vec_best
        ),
        {ftsBlock}
        rrf AS (
            SELECT
                COALESCE(v."DocumentId", f."DocumentId")              AS doc_id,
                v.chunk_text,
                COALESCE(1.0 / ({RrfK}.0 + v.rank::float8), 0)
                + COALESCE(1.0 / ({RrfK}.0 + f.rank::float8), 0)     AS rrf_score
            FROM       vec_ranked v
            FULL OUTER JOIN fts_ranked f ON v."DocumentId" = f."DocumentId"
        )
        SELECT r.doc_id, r.chunk_text, r.rrf_score
        FROM   rrf r
        JOIN   "Documents" d ON d."Id" = r.doc_id
        WHERE  1=1 {ownerFilter}
        ORDER  BY r.rrf_score DESC
        LIMIT  @size OFFSET @offset
        """;

    await db.Database.OpenConnectionAsync(ct);
    try
    {
      var conn    = (NpgsqlConnection)db.Database.GetDbConnection();
      var matches = new List<(DocumentId Id, double Score, string? ChunkText)>();

      using (var cmd = new NpgsqlCommand(sql, conn))
      {
        cmd.Parameters.AddWithValue("threshold", DistanceThreshold);
        cmd.Parameters.AddWithValue("size",      size);
        cmd.Parameters.AddWithValue("offset",    (page - 1) * size);
        if (!string.IsNullOrEmpty(tsQuery))
          cmd.Parameters.AddWithValue("tsQuery", tsQuery);
        if (ownerId.HasValue)
          cmd.Parameters.AddWithValue("ownerId", ownerId.Value);

        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
          var chunkText = reader.IsDBNull(1) ? null : reader.GetString(1);
          matches.Add((new DocumentId(reader.GetGuid(0)), reader.GetDouble(2), chunkText));
        }
      }

      if (matches.Count == 0)
        return new Page<SearchResultItem>([], page, size, 0);

      var matchedIds = matches.Select(m => m.Id).ToList();
      var docs       = await db.Documents
        .Include(d => d.Tags)
        .Where(d => matchedIds.Contains(d.Id))
        .ToListAsync(ct);

      var metaById = matches.ToDictionary(m => m.Id);
      var items = docs
        .OrderByDescending(d => metaById[d.Id].Score)
        .Select(d => new SearchResultItem(
            d,
            Math.Round(metaById[d.Id].Score, 6),
            metaById[d.Id].ChunkText))
        .ToList();

      return new Page<SearchResultItem>(items, page, size, items.Count);
    }
    finally
    {
      await db.Database.CloseConnectionAsync();
    }
  }

  private static string BuildTsQuery(string[] terms)
  {
    var safe = terms
      .Select(t => SafeTerm().Replace(t, ""))
      .Where(t => !string.IsNullOrEmpty(t))
      .Select(t => t + ":*");
    return string.Join(" | ", safe);
  }

  private static string ToVectorLiteral(float[] v)
  {
    var values = string.Join(",", v.Select(f => f.ToString("G9", CultureInfo.InvariantCulture)));
    return $"'[{values}]'::vector";
  }

  [GeneratedRegex(@"[^a-zA-Z0-9\u00C0-\u024F\-_]")]
  private static partial Regex SafeTerm();
}
