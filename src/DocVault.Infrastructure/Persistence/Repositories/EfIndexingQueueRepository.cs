using DocVault.Application.Abstractions.Persistence;
using DocVault.Application.Background.Queue;

namespace DocVault.Infrastructure.Persistence.Repositories;

/// <summary>
/// Stages a new <see cref="IndexingQueueEntry"/> on the shared <see cref="DocVaultDbContext"/>
/// without flushing. Changes are committed atomically by the surrounding unit-of-work transaction,
/// ensuring the queue row is always created together with the <c>ImportJob</c> row.
/// </summary>
public sealed class EfIndexingQueueRepository : IIndexingQueueRepository
{
  private readonly DocVaultDbContext _db;

  public EfIndexingQueueRepository(DocVaultDbContext db) => _db = db;

  public async Task AddAsync(IndexingWorkItem item, CancellationToken cancellationToken = default)
  {
    await _db.IndexingQueue.AddAsync(new IndexingQueueEntry
    {
      JobId       = item.JobId,
      StoragePath = item.StoragePath,
      ContentType = item.ContentType,
      EnqueuedAt  = DateTime.UtcNow,
    }, cancellationToken);
  }
}
