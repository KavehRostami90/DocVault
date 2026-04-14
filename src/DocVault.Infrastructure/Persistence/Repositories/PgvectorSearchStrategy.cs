using System.Globalization;
using DocVault.Application.Common.Paging;
using DocVault.Application.UseCases.Search;
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

    var sql = $"""
        SELECT d.* FROM "Documents" d
        WHERE d."Embedding" IS NOT NULL
        {ownerFilter}
        ORDER BY d."Embedding" <=> {vectorLiteral}
        LIMIT @size OFFSET @offset
        """;

    var parameters = new List<NpgsqlParameter>
    {
      new("size",   size),
      new("offset", (page - 1) * size),
    };
    if (ownerId.HasValue)
      parameters.Add(new NpgsqlParameter("ownerId", ownerId.Value));

    var docs = await db.Documents
      .FromSqlRaw(sql, parameters.Cast<object>().ToArray())
      .Include(d => d.Tags)
      .ToListAsync(ct);

    // Rank results by position — first result is most similar.
    var total = docs.Count;
    var items = docs
      .Select((d, i) => new SearchResultItem(d, Math.Round(1.0 - (double)i / Math.Max(total, 1), 4)))
      .ToList();

    return new Page<SearchResultItem>(items, page, size, total);
  }

  // Formats a float[] as a pgvector literal safe to embed in SQL: '[1.234,-0.567,...]'::vector
  private static string ToVectorLiteral(float[] v)
  {
    var values = string.Join(",", v.Select(f => f.ToString("G9", CultureInfo.InvariantCulture)));
    return $"'[{values}]'::vector";
  }
}
