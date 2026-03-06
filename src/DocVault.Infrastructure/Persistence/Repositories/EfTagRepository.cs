using DocVault.Application.Abstractions.Persistence;
using DocVault.Domain.Documents;
using Microsoft.EntityFrameworkCore;

namespace DocVault.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core-backed tag repository.
/// </summary>
public class EfTagRepository : ITagRepository
{
  private readonly DocVaultDbContext _db;

  public EfTagRepository(DocVaultDbContext db)
  {
    _db = db;
  }

  /// <summary>
  /// Adds a new tag and saves changes.
  /// </summary>
  /// <param name="tag">Tag entity to persist.</param>
  /// <param name="cancellationToken">Cancellation token.</param>
  public async Task AddAsync(Tag tag, CancellationToken cancellationToken = default)
  {
    await _db.Tags.AddAsync(tag, cancellationToken);
    await _db.SaveChangesAsync(cancellationToken);
  }

  /// <summary>
  /// Gets a tag by its normalized name.
  /// </summary>
  /// <param name="name">Tag name.</param>
  /// <param name="cancellationToken">Cancellation token.</param>
  public Task<Tag?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    => _db.Tags.FirstOrDefaultAsync(t => t.Name == name, cancellationToken);

  /// <summary>
  /// Gets tags by their names.
  /// </summary>
  /// <param name="names">Collection of names to match.</param>
  /// <param name="cancellationToken">Cancellation token.</param>
  public async Task<IReadOnlyCollection<Tag>> GetByNamesAsync(IEnumerable<string> names, CancellationToken cancellationToken = default)
  {
    var list = await _db.Tags.Where(t => names.Contains(t.Name)).ToListAsync(cancellationToken);
    return list;
  }

  /// <summary>
  /// Lists all tags ordered by name.
  /// </summary>
  /// <param name="cancellationToken">Cancellation token.</param>
  public async Task<IReadOnlyCollection<Tag>> ListAsync(CancellationToken cancellationToken = default)
  {
    var list = await _db.Tags
      .AsNoTracking()
      .OrderBy(t => t.Name)
      .ToListAsync(cancellationToken);
    return list;
  }
}
