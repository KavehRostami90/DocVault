using System.Globalization;
using DocVault.Application.Common.Paging;
using DocVault.Application.UseCases.Search;
using DocVault.Domain.Documents;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DocVault.Infrastructure.Persistence.Repositories;

/// <summary>
/// Semantic search strategy using pgvector cosine similarity (<c>&lt;=&gt;</c> operator).
/// Selected when a query embedding is available and the database is PostgreSQL.
/// Falls back to <see cref="PostgresSearchStrategy"/> (full-text search) when no embedding
/// is provided, or to <see cref="InMemorySearchStrategy"/> for non-relational providers.
/// </summary>
internal sealed class PgvectorSearchStrategy : IDocumentSearchStrategy
{
  // Cosine distance cutoff (0 = identical, 2 = opposite vectors).
  // Documents at or beyond this distance are excluded as irrelevant.
  private const double DistanceThreshold = 0.7;

  // Only activate when we have a real embedding AND a relational (Postgres) database.
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
    // queryVector is non-null here — CanHandle ensures it.
    // Float values formatted with InvariantCulture contain only digits, '.', '-', ',' — no injection risk.
    var vectorLiteral = ToVectorLiteral(queryVector!);

    var ownerFilter = ownerId.HasValue ? "AND d.\"OwnerId\" = @ownerId" : string.Empty;

    // Step 1: fetch (Id, actual cosine distance) pairs via ADO.NET so we get the real
    // distance value back — EF Core's FromSqlRaw only maps to entity types and would
    // discard the computed distance column.
    var sql = $"""
        SELECT d."Id", (d."Embedding" <=> {vectorLiteral}) AS distance
        FROM "Documents" d
        WHERE d."Embedding" IS NOT NULL
          AND (d."Embedding" <=> {vectorLiteral}) < @threshold
          {ownerFilter}
        ORDER BY distance
        LIMIT @size OFFSET @offset
        """;

    await db.Database.OpenConnectionAsync(ct);
    try
    {
      var conn = (NpgsqlConnection)db.Database.GetDbConnection();
      var idDistances = new List<(DocumentId Id, double Distance)>();

      using (var cmd = new NpgsqlCommand(sql, conn))
      {
        cmd.Parameters.AddWithValue("threshold", DistanceThreshold);
        cmd.Parameters.AddWithValue("size", size);
        cmd.Parameters.AddWithValue("offset", (page - 1) * size);
        if (ownerId.HasValue)
          cmd.Parameters.AddWithValue("ownerId", ownerId.Value);

        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
          idDistances.Add((new DocumentId(reader.GetGuid(0)), reader.GetDouble(1)));
      }

      if (idDistances.Count == 0)
        return new Page<SearchResultItem>([], page, size, 0);

      // Step 2: load full document entities (with tags) for the matched IDs.
      var matchedIds = idDistances.Select(p => p.Id).ToList();
      var docs = await db.Documents
        .Include(d => d.Tags)
        .Where(d => matchedIds.Contains(d.Id))
        .ToListAsync(ct);

      // Step 3: sort by actual distance and convert distance → score (1.0 = identical).
      var distanceById = idDistances.ToDictionary(p => p.Id, p => p.Distance);
      var items = docs
        .OrderBy(d => distanceById[d.Id])
        .Select(d => new SearchResultItem(d, Math.Round(1.0 - distanceById[d.Id], 4)))
        .ToList();

      return new Page<SearchResultItem>(items, page, size, items.Count);
    }
    finally
    {
      await db.Database.CloseConnectionAsync();
    }
  }

  // Formats a float[] as a pgvector literal safe to embed in SQL: '[1.234,-0.567,...]'::vector
  private static string ToVectorLiteral(float[] v)
  {
    var values = string.Join(",", v.Select(f => f.ToString("G9", CultureInfo.InvariantCulture)));
    return $"'[{values}]'::vector";
  }
}
