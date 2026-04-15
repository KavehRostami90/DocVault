using DocVault.Application.Abstractions.Persistence;
using DocVault.Domain.Documents;
using Microsoft.EntityFrameworkCore;

namespace DocVault.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of <see cref="ITagRepository"/>.
/// Provides read and write operations for <see cref="Tag"/> entities.
/// </summary>
public class EfTagRepository : ITagRepository
{
  private readonly DocVaultDbContext _db;

  /// <summary>Initialises the repository with the scoped database context.</summary>
  /// <param name="db">The EF Core database context for this request scope.</param>
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
  /// Lists tags ordered by name.
  /// When <paramref name="ownerId"/> is provided, only tags used in that user's documents are returned.
  /// </summary>
  /// <param name="ownerId">Optional owner filter. Pass <c>null</c> to return all tags (admin view).</param>
  /// <param name="cancellationToken">Cancellation token.</param>
  public async Task<IReadOnlyCollection<Tag>> ListAsync(Guid? ownerId = null, CancellationToken cancellationToken = default)
  {
    var query = _db.Tags.AsNoTracking();

    if (ownerId.HasValue)
    {
      var ownerTagIds = _db.Documents
        .Where(d => d.OwnerId == ownerId)
        .SelectMany(d => d.Tags)
        .Select(t => t.Id);

      query = query.Where(t => ownerTagIds.Contains(t.Id));
    }

    var list = await query.OrderBy(t => t.Name).ToListAsync(cancellationToken);
    return list;
  }
}
