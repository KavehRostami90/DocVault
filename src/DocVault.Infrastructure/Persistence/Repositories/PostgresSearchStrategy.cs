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
      .Include(d => d.Tags)
      .Where(d => EF.Functions.ToTsVector("english", d.Text)
        .Matches(EF.Functions.ToTsQuery("english", tsQuery)));

    if (ownerId.HasValue)
      query = query.Where(d => d.OwnerId == ownerId);

    var docs = await query.ToListAsync(ct);

    var items = docs
      .Select(d => new SearchResultItem(d, DocumentScorer.Compute(d, terms)))
      .OrderByDescending(i => i.Score)
      .Skip((page - 1) * size)
      .Take(size)
      .ToList();

    return new Page<SearchResultItem>(items, page, size, docs.Count);
  }

  // Strips tsquery special chars and appends :* for prefix matching.
  private static string SanitizeTerm(string term)
  {
    var safe = Regex.Replace(term, @"[^a-zA-Z0-9\u00C0-\u024F\-_]", "");
    return string.IsNullOrEmpty(safe) ? string.Empty : safe + ":*";
  }
}
