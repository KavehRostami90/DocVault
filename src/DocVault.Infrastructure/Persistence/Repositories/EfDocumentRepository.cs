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
/// Repositories only stage changes via the EF change tracker; callers flush via <see cref="IUnitOfWork"/>.
/// Search is delegated to the first matching <see cref="IDocumentSearchStrategy"/> (registered by priority).
/// </summary>
internal class EfDocumentRepository : IDocumentRepository
{
  private readonly DocVaultDbContext _db;
  private readonly IEnumerable<IDocumentSearchStrategy> _strategies;

  private static readonly IReadOnlyDictionary<string, Func<string, Expression<Func<Document, bool>>>>
    _filterRegistry = DocumentFilterRegistry.Build();

  public EfDocumentRepository(DocVaultDbContext db, IEnumerable<IDocumentSearchStrategy> strategies)
  {
    _db         = db;
    _strategies = strategies;
  }

  public async Task AddAsync(Document document, CancellationToken cancellationToken = default)
    => await _db.Documents.AddAsync(document, cancellationToken);

  public Task DeleteAsync(Document document, CancellationToken cancellationToken = default)
  {
    _db.Documents.Remove(document);
    return Task.CompletedTask;
  }

  public Task<Document?> GetAsync(DocumentId id, CancellationToken cancellationToken = default)
    => _db.Documents.Include(d => d.Tags).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

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

  public Task UpdateAsync(Document document, CancellationToken cancellationToken = default)
  {
    _db.Documents.Update(document);
    return Task.CompletedTask;
  }

  public async Task<Page<SearchResultItem>> SearchAsync(string query, int page, int size, Guid? ownerId = null, float[]? queryVector = null, CancellationToken cancellationToken = default)
  {
    var terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    if (terms.Length == 0 && queryVector is null)
      return new Page<SearchResultItem>([], page, size, 0);

    var strategy = _strategies.First(s => s.CanHandle(_db, queryVector, terms));
    return await strategy.SearchAsync(_db, terms, page, size, ownerId, queryVector, cancellationToken);
  }

  public async Task<Dictionary<string, long>> GetCountsByStatusAsync(CancellationToken cancellationToken = default)
  {
    var rows = await _db.Documents
      .GroupBy(d => d.Status)
      .Select(g => new { Status = g.Key, Count = (long)g.Count() })
      .ToListAsync(cancellationToken);

    return rows.ToDictionary(x => x.Status.ToString(), x => x.Count);
  }
}
