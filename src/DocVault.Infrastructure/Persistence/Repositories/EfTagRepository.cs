using DocVault.Application.Abstractions.Persistence;
using DocVault.Domain.Documents;
using Microsoft.EntityFrameworkCore;

namespace DocVault.Infrastructure.Persistence.Repositories;

public class EfTagRepository : ITagRepository
{
  private readonly DocVaultDbContext _db;

  public EfTagRepository(DocVaultDbContext db)
  {
    _db = db;
  }

  public async Task AddAsync(Tag tag, CancellationToken cancellationToken = default)
  {
    await _db.Tags.AddAsync(tag, cancellationToken);
    await _db.SaveChangesAsync(cancellationToken);
  }

  public Task<Tag?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    => _db.Tags.FirstOrDefaultAsync(t => t.Name == name, cancellationToken);

  public async Task<IReadOnlyCollection<Tag>> GetByNamesAsync(IEnumerable<string> names, CancellationToken cancellationToken = default)
  {
    var list = await _db.Tags.Where(t => names.Contains(t.Name)).ToListAsync(cancellationToken);
    return list;
  }

  public async Task<IReadOnlyCollection<Tag>> ListAsync(CancellationToken cancellationToken = default)
  {
    var list = await _db.Tags
      .AsNoTracking()
      .OrderBy(t => t.Name)
      .ToListAsync(cancellationToken);
    return list;
  }
}
