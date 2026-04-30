using System.Text.RegularExpressions;
using DocVault.Application.Common.Paging;
using DocVault.Application.UseCases.Search;
using DocVault.Domain.Documents;
using Microsoft.EntityFrameworkCore;

namespace DocVault.Infrastructure.Persistence.Repositories;

/// <summary>
/// Postgres full-text search strategy using <c>to_tsvector</c> / <c>to_tsquery</c>.
/// Each term is sanitised to prevent tsquery injection and suffixed with <c>:*</c>
/// for prefix matching.
/// </summary>
internal sealed class PostgresSearchStrategy : IDocumentSearchStrategy
{
  // Handles relational keyword searches; vector search is handled by PgvectorSearchStrategy.
  public bool CanHandle(DocVaultDbContext db, float[]? queryVector, string[] terms) => db.Database.IsRelational();

  public async Task<Page<SearchResultItem>> SearchAsync(
    DocVaultDbContext db,
    string[] terms,
    int page,
    int size,
    Guid? ownerId,
    float[]? queryVector,
    CancellationToken ct)
  {
    var tsQuery = string.Join(" | ", terms
      .Select(SanitizeTerm)
      .Where(t => !string.IsNullOrEmpty(t)));

    if (string.IsNullOrEmpty(tsQuery))
      return new Page<SearchResultItem>([], page, size, 0);

    var query = db.Documents
      .Where(d => EF.Functions.ToTsVector("english", d.Text)
        .Matches(EF.Functions.ToTsQuery("english", tsQuery)));

    if (ownerId.HasValue)
      query = query.Where(d => d.OwnerId == ownerId);

    var totalCount = await query.LongCountAsync(ct);
    if (totalCount == 0)
      return new Page<SearchResultItem>([], page, size, 0);

    var projected = await query
      .OrderByDescending(d => EF.Functions.ToTsVector("english", d.Text)
          .Rank(EF.Functions.ToTsQuery("english", tsQuery)))
      .Skip((page - 1) * size)
      .Take(size)
      .Select(d => new
      {
        d.Id,
        d.Title,
        d.FileName,
        Tags = d.Tags.Select(t => t.Name).ToList()
      })
      .ToListAsync(ct);

    var items = projected
      .Select(r => new SearchResultItem(
          new DocumentSearchSummary(r.Id, r.Title, r.FileName, r.Tags),
          1.0))
      .ToList();

    return new Page<SearchResultItem>(items, page, size, totalCount);
  }

  // Strips tsquery special chars and appends :* for prefix matching.
  private static string SanitizeTerm(string term)
  {
    var safe = Regex.Replace(term, @"[^a-zA-Z0-9\u00C0-\u024F\-_]", "");
    return string.IsNullOrEmpty(safe) ? string.Empty : safe + ":*";
  }
}
