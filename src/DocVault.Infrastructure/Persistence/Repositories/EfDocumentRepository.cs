using System.Linq.Expressions;
using DocVault.Application.Abstractions.Persistence;
using DocVault.Application.Common.Filtering;
using DocVault.Application.Common.Paging;
using DocVault.Application.UseCases.Search;
using DocVault.Domain.Common;
using DocVault.Domain.Documents;
using Microsoft.EntityFrameworkCore;
using NpgsqlTypes;

namespace DocVault.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IDocumentRepository"/>.
/// Provides CRUD operations, paginated listing with dynamic filter/sort,
/// and keyword-based full-text search using expression trees.
/// </summary>
public class EfDocumentRepository : IDocumentRepository
{
  private readonly DocVaultDbContext _db;

  /// <summary>Initialises the repository with the scoped database context.</summary>
  /// <param name="db">The EF Core database context for this request scope.</param>
  public EfDocumentRepository(DocVaultDbContext db)
  {
    _db = db;
  }

  /// <summary>Persists a new <see cref="Document"/> and saves changes.</summary>
  /// <param name="document">The document aggregate to add.</param>
  /// <param name="cancellationToken">Cancellation token.</param>
  public async Task AddAsync(Document document, CancellationToken cancellationToken = default)
  {
    await _db.Documents.AddAsync(document, cancellationToken);
    await _db.SaveChangesAsync(cancellationToken);
  }

  /// <summary>Removes a <see cref="Document"/> and saves changes.</summary>
  /// <param name="document">The document aggregate to delete.</param>
  /// <param name="cancellationToken">Cancellation token.</param>
  public async Task DeleteAsync(Document document, CancellationToken cancellationToken = default)
  {
    _db.Documents.Remove(document);
    await _db.SaveChangesAsync(cancellationToken);
  }

  /// <summary>Retrieves a single <see cref="Document"/> by its identifier, including its tags.</summary>
  /// <param name="id">The document identifier.</param>
  /// <param name="cancellationToken">Cancellation token.</param>
  /// <returns>The matching document, or <c>null</c> if not found.</returns>
  public Task<Document?> GetAsync(DocumentId id, CancellationToken cancellationToken = default)
    => _db.Documents.Include(d => d.Tags).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

  /// <summary>
  /// Returns a paginated, filtered, and sorted page of documents.
  /// Supported filter keys: <c>title</c>, <c>status</c>, <c>tag</c>.
  /// Supported sort keys: <c>title</c>, <c>fileName</c>, <c>size</c>, <c>status</c>, <c>createdAt</c>, <c>updatedAt</c>.
  /// </summary>
  /// <param name="request">Pagination, filter, and sort parameters.</param>
  /// <param name="cancellationToken">Cancellation token.</param>
  /// <returns>A <see cref="Page{T}"/> containing the matching documents.</returns>
  public async Task<Page<Document>> ListAsync(PageRequest request, Guid? ownerId = null, CancellationToken cancellationToken = default)
  {
    var query = _db.Documents.Include(d => d.Tags).AsQueryable();

    if (ownerId.HasValue)
      query = query.Where(d => d.OwnerId == ownerId);

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
      [ValidationConstants.Paging.SortFields.TITLE]      = d => d.Title,
      [ValidationConstants.Paging.SortFields.FILE_NAME]  = d => d.FileName,
      [ValidationConstants.Paging.SortFields.SIZE]       = d => d.Size,
      [ValidationConstants.Paging.SortFields.STATUS]     = d => d.Status,
      [ValidationConstants.Paging.SortFields.CREATED_AT] = d => d.CreatedAt,
      [ValidationConstants.Paging.SortFields.UPDATED_AT] = d => d.UpdatedAt ?? d.CreatedAt,
    };

    query = SortBuilder.Apply(query, request.Sort, request.Desc, sortRegistry, d => d.CreatedAt);

    var total = await query.LongCountAsync(cancellationToken);
    var items = await query.Skip((request.Page - 1) * request.Size).Take(request.Size).ToListAsync(cancellationToken);
    return new Page<Document>(items, request.Page, request.Size, total);
  }

  /// <summary>Updates an existing <see cref="Document"/> and saves changes.</summary>
  /// <param name="document">The document aggregate with updated state.</param>
  /// <param name="cancellationToken">Cancellation token.</param>
  public async Task UpdateAsync(Document document, CancellationToken cancellationToken = default)
  {
    _db.Documents.Update(document);
    await _db.SaveChangesAsync(cancellationToken);
  }

  /// <summary>
  /// Performs an in-database keyword search across document titles and extracted text.
  /// Each term is OR-ed; results are ranked by a lightweight relevance score
  /// (title match = 1.0, body match = 0.3) and returned as a paginated page.
  /// </summary>
  /// <param name="query">Whitespace-delimited search terms.</param>
  /// <param name="page">1-based page number.</param>
  /// <param name="size">Page size.</param>
  /// <param name="cancellationToken">Cancellation token.</param>
  /// <returns>A <see cref="Page{T}"/> of ranked <see cref="SearchResultItem"/> objects.</returns>
  public async Task<Page<SearchResultItem>> SearchAsync(string query, int page, int size, Guid? ownerId = null, CancellationToken cancellationToken = default)
  {
    var terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    if (terms.Length == 0)
    {
      return new Page<SearchResultItem>([], page, size, 0);
    }

    // Use PostgreSQL full-text search when running against a real database.
    // The OR-prefix query (term:*) enables prefix matching on each token.
    if (_db.Database.IsRelational())
    {
      var tsQuery = string.Join(" | ", terms.Select(t => t.Replace("'", "''") + ":*"));

      var docQuery = _db.Documents
        .Include(d => d.Tags)
        .Where(d => EF.Functions.ToTsVector("english", d.Text).Matches(
          EF.Functions.ToTsQuery("english", tsQuery)));

      if (ownerId.HasValue)
        docQuery = docQuery.Where(d => d.OwnerId == ownerId);

      var docs = await docQuery.ToListAsync(cancellationToken);

      var items = docs
        .Select(d => new SearchResultItem(d, ComputeScore(d, terms)))
        .OrderByDescending(i => i.Score)
        .Skip((page - 1) * size)
        .Take(size)
        .ToList();

      return new Page<SearchResultItem>(items, page, size, docs.Count);
    }

    // InMemory fallback used in tests — LIKE-style OR across title and text.
    IQueryable<Document> q = _db.Documents.Include(d => d.Tags);
    q = q.Where(BuildTermsFilter(terms));
    if (ownerId.HasValue)
      q = q.Where(d => d.OwnerId == ownerId);
    var total = await q.LongCountAsync(cancellationToken);
    var fallbackDocs = await q.ToListAsync(cancellationToken);
    var fallbackItems = fallbackDocs
      .Select(d => new SearchResultItem(d, ComputeScore(d, terms)))
      .OrderByDescending(item => item.Score)
      .Skip((page - 1) * size)
      .Take(size)
      .ToList();
    return new Page<SearchResultItem>(fallbackItems, page, size, total);
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

  public async Task<Dictionary<string, long>> GetCountsByStatusAsync(CancellationToken cancellationToken = default)
  {
    var rows = await _db.Documents
      .GroupBy(d => d.Status)
      .Select(g => new { Status = g.Key, Count = (long)g.Count() })
      .ToListAsync(cancellationToken);

    return rows.ToDictionary(x => x.Status.ToString(), x => x.Count);
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
