using System.Linq.Expressions;
using System.Reflection;
using DocVault.Application.Abstractions.Persistence;
using DocVault.Application.Common.Filtering;
using DocVault.Application.Common.Paging;
using DocVault.Application.UseCases.Search;
using DocVault.Domain.Documents;
using Microsoft.EntityFrameworkCore;

namespace DocVault.Infrastructure.Persistence.Repositories;

public class EfDocumentRepository : IDocumentRepository
{
  private readonly DocVaultDbContext _db;

  public EfDocumentRepository(DocVaultDbContext db)
  {
    _db = db;
  }

  public async Task AddAsync(Document document, CancellationToken cancellationToken = default)
  {
    await _db.Documents.AddAsync(document, cancellationToken);
    await _db.SaveChangesAsync(cancellationToken);
  }

  public async Task DeleteAsync(Document document, CancellationToken cancellationToken = default)
  {
    _db.Documents.Remove(document);
    await _db.SaveChangesAsync(cancellationToken);
  }

  public Task<Document?> GetAsync(DocumentId id, CancellationToken cancellationToken = default)
    => _db.Documents.Include(d => d.Tags).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

  public async Task<Page<Document>> ListAsync(PageRequest request, CancellationToken cancellationToken = default)
  {
    var query = _db.Documents.Include(d => d.Tags).AsQueryable();

    var filterRegistry = new Dictionary<string, Func<string, Expression<Func<Document, bool>>>>
    {
      ["title"] = value => d => d.Title.Contains(value),
      ["status"] = value =>
      {
        if (Enum.TryParse<DocumentStatus>(value, true, out var status))
        {
          return d => d.Status == status;
        }
        return d => true;
      },
      ["tag"] = value => d => d.Tags.Any(t => t.Name.Contains(value))
    };

    query = FilterBuilder.Apply(query, request.Filters, filterRegistry);

    var sortRegistry = new Dictionary<string, Expression<Func<Document, object>>>
    {
      ["title"] = d => d.Title,
      ["filename"] = d => d.FileName,
      ["status"] = d => d.Status,
      ["created"] = d => d.CreatedAt,
      ["updated"] = d => d.UpdatedAt ?? d.CreatedAt
    };

    query = SortBuilder.Apply(query, request.Sort, request.Desc, sortRegistry, d => d.CreatedAt);

    var total = await query.LongCountAsync(cancellationToken);
    var items = await query.Skip((request.Page - 1) * request.Size).Take(request.Size).ToListAsync(cancellationToken);
    return new Page<Document>(items, request.Page, request.Size, total);
  }

  public async Task UpdateAsync(Document document, CancellationToken cancellationToken = default)
  {
    _db.Documents.Update(document);
    await _db.SaveChangesAsync(cancellationToken);
  }

  public async Task<Page<SearchResultItem>> SearchAsync(string query, int page, int size, CancellationToken cancellationToken = default)
  {
    var terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    if (terms.Length == 0)
    {
      return new Page<SearchResultItem>([], page, size, 0);
    }

    // Build an OR expression tree EF can translate for both the InMemory and
    // relational providers.  Each term produces:
    //   d => d.Title.Contains(term) || d.Text.Contains(term)
    // and the per-term predicates are OR-ed together.
    IQueryable<Document> q = _db.Documents.Include(d => d.Tags);
    q = q.Where(BuildTermsFilter(terms));

    var total = await q.LongCountAsync(cancellationToken);
    var docs  = await q.ToListAsync(cancellationToken);

    var items = docs.Select(d => new SearchResultItem(d, ComputeScore(d, terms)))
                    .OrderByDescending(item => item.Score)
                    .Skip((page - 1) * size)
                    .Take(size)
                    .ToList();
    return new Page<SearchResultItem>(items, page, size, total);
  }

  // Builds: d => (d.Title.Contains(t1) || d.Text.Contains(t1))
  //           || (d.Title.Contains(t2) || d.Text.Contains(t2)) ...
  private static Expression<Func<Document, bool>> BuildTermsFilter(string[] terms)
  {
    var param        = Expression.Parameter(typeof(Document), "d");
    var titleProp    = Expression.Property(param, nameof(Document.Title));
    var textProp     = Expression.Property(param, nameof(Document.Text));
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

  // Lightweight keyword relevance: title hits worth 1.0 each, body hits worth 0.3 each, normalised to [0, 1].
  private static double ComputeScore(Document doc, string[] terms)
  {
    var titleHits = terms.Count(t => doc.Title.Contains(t, StringComparison.OrdinalIgnoreCase));
    var textHits  = terms.Count(t => doc.Text.Contains(t, StringComparison.OrdinalIgnoreCase));
    var maxPossible = terms.Length * 1.3;
    return Math.Round((titleHits * 1.0 + textHits * 0.3) / maxPossible, 4);
  }
}
