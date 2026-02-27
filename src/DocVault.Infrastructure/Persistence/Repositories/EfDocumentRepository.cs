using System.Linq.Expressions;
using DocVault.Application.Abstractions.Persistence;
using DocVault.Application.Common.Filtering;
using DocVault.Application.Common.Paging;
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
}
