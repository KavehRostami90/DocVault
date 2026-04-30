using System.Linq.Expressions;
using DocVault.Application.Common.Paging;
using DocVault.Application.UseCases.Search;
using DocVault.Domain.Documents;
using Microsoft.EntityFrameworkCore;

namespace DocVault.Infrastructure.Persistence.Repositories;

/// <summary>
/// In-memory LIKE-style search strategy used by the EF Core in-memory provider
/// (integration tests and local development without Postgres).
/// </summary>
internal sealed class InMemorySearchStrategy : IDocumentSearchStrategy
{
  public bool CanHandle(DocVaultDbContext db, float[]? queryVector, string[] terms) => true; // fallback — always matches

  public async Task<Page<SearchResultItem>> SearchAsync(
    DocVaultDbContext db,
    string[] terms,
    int page,
    int size,
    Guid? ownerId,
    float[]? queryVector,
    CancellationToken ct)
  {
    // InMemory provider cannot do vector search; if no text terms were provided there is
    // nothing to match against so return an empty page instead of crashing in BuildOrFilter.
    if (terms.Length == 0)
      return new Page<SearchResultItem>([], page, size, 0);

    IQueryable<Document> query = db.Documents.Include(d => d.Tags);
    query = query.Where(BuildOrFilter(terms));

    if (ownerId.HasValue)
      query = query.Where(d => d.OwnerId == ownerId);

    // Load all matching docs to score them in-memory, then paginate.
    // Text is required by DocumentScorer; Tags are required for the result DTO.
    var docs = await query.ToListAsync(ct);

    var items = docs
      .Select(d => new SearchResultItem(
          new DocumentSearchSummary(d.Id, d.Title, d.FileName, d.Tags.Select(t => t.Name).ToList()),
          DocumentScorer.Compute(d, terms)))
      .OrderByDescending(i => i.Score)
      .ToList();

    var paged = items
      .Skip((page - 1) * size)
      .Take(size)
      .ToList();

    return new Page<SearchResultItem>(paged, page, size, (long)items.Count);
  }

  // Builds: d => (d.Title.Contains(t1) || d.Text.Contains(t1)) || ...
  private static Expression<Func<Document, bool>> BuildOrFilter(string[] terms)
  {
    var param       = Expression.Parameter(typeof(Document), "d");
    var titleProp   = Expression.Property(param, nameof(Document.Title));
    var textProp    = Expression.Property(param, nameof(Document.Text));
    var containsMeth = typeof(string).GetMethod(nameof(string.Contains), [typeof(string)])!;

    Expression? body = null;
    foreach (var term in terms)
    {
      var literal    = Expression.Constant(term);
      var titleHit   = Expression.Call(titleProp, containsMeth, literal);
      var textHit    = Expression.Call(textProp,  containsMeth, literal);
      var termClause = Expression.OrElse(titleHit, textHit);
      body = body is null ? termClause : Expression.OrElse(body, termClause);
    }

    return Expression.Lambda<Func<Document, bool>>(body!, param);
  }
}
