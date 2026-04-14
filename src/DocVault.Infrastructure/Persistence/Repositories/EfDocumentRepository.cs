using System.Linq.Expressions;
using DocVault.Application.Abstractions.Persistence;
using DocVault.Application.Common.Filtering;
using DocVault.Application.Common.Paging;
using DocVault.Application.UseCases.Search;
using DocVault.Domain.Common;
using DocVault.Domain.Documents;
using Microsoft.EntityFrameworkCore;

namespace DocVault.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IDocumentRepository"/>.
/// Provides CRUD operations, paginated listing with dynamic filter/sort,
/// and keyword-based full-text search delegated to <see cref="IDocumentSearchStrategy"/> implementations.
/// </summary>
public class EfDocumentRepository : IDocumentRepository
{
  private readonly DocVaultDbContext _db;

  // Filter predicates are built once and reused across requests.
  private static readonly IReadOnlyDictionary<string, Func<string, Expression<Func<Document, bool>>>>
    _filterRegistry = DocumentFilterRegistry.Build();

  // Strategies are tried in order; the first whose CanHandle returns true wins.
  // PgvectorSearchStrategy handles relational + embedding; PostgresSearchStrategy handles relational FTS fallback.
  private static readonly IReadOnlyList<IDocumentSearchStrategy> _searchStrategies =
  [
    new PgvectorSearchStrategy(),
    new PostgresSearchStrategy(),
    new InMemorySearchStrategy(),
  ];

  /// <summary>Initialises the repository with the scoped database context.</summary>
  /// <param name="db">The EF Core database context for this request scope.</param>
  public EfDocumentRepository(DocVaultDbContext db)
  {
    _db = db;
  }

  /// <summary>Persists a new <see cref="Document"/> and saves changes.</summary>
  public async Task AddAsync(Document document, CancellationToken cancellationToken = default)
  {
    await _db.Documents.AddAsync(document, cancellationToken);
    await _db.SaveChangesAsync(cancellationToken);
  }

  /// <summary>Removes a <see cref="Document"/> and saves changes.</summary>
  public async Task DeleteAsync(Document document, CancellationToken cancellationToken = default)
  {
    _db.Documents.Remove(document);
    await _db.SaveChangesAsync(cancellationToken);
  }

  /// <summary>Retrieves a single <see cref="Document"/> by its identifier, including its tags.</summary>
  public Task<Document?> GetAsync(DocumentId id, CancellationToken cancellationToken = default)
    => _db.Documents.Include(d => d.Tags).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

  /// <summary>
  /// Returns a paginated, filtered, and sorted page of documents.
  /// Supported filter keys: <c>title</c>, <c>status</c>, <c>tag</c>.
  /// Supported sort keys: <c>title</c>, <c>fileName</c>, <c>size</c>, <c>status</c>, <c>createdAt</c>, <c>updatedAt</c>.
  /// </summary>
  public async Task<Page<Document>> ListAsync(PageRequest request, Guid? ownerId = null, CancellationToken cancellationToken = default)
  {
    var query = _db.Documents.Include(d => d.Tags).AsQueryable();

    if (ownerId.HasValue)
      query = query.Where(d => d.OwnerId == ownerId);

    query = FilterBuilder.Apply(query, request.Filters, _filterRegistry);

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
  public async Task UpdateAsync(Document document, CancellationToken cancellationToken = default)
  {
    _db.Documents.Update(document);
    await _db.SaveChangesAsync(cancellationToken);
  }

  /// <summary>
  /// Searches documents using the best available strategy for the active database provider.
  /// When <paramref name="queryVector"/> is provided and the database is PostgreSQL with pgvector,
  /// semantic cosine-similarity search is used; otherwise falls back to full-text search.
  /// </summary>
  public async Task<Page<SearchResultItem>> SearchAsync(string query, int page, int size, Guid? ownerId = null, float[]? queryVector = null, CancellationToken cancellationToken = default)
  {
    var terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    if (terms.Length == 0 && queryVector is null)
      return new Page<SearchResultItem>([], page, size, 0);

    var strategy = _searchStrategies.First(s => s.CanHandle(_db, queryVector));
    return await strategy.SearchAsync(_db, terms, page, size, ownerId, queryVector, cancellationToken);
  }

  /// <summary>Returns document counts grouped by processing status.</summary>
  public async Task<Dictionary<string, long>> GetCountsByStatusAsync(CancellationToken cancellationToken = default)
  {
    var rows = await _db.Documents
      .GroupBy(d => d.Status)
      .Select(g => new { Status = g.Key, Count = (long)g.Count() })
      .ToListAsync(cancellationToken);

    return rows.ToDictionary(x => x.Status.ToString(), x => x.Count);
  }
}
