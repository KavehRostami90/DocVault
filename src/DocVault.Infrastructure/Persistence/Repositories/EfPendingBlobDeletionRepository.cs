using DocVault.Application.Abstractions.Persistence;
using DocVault.Domain.Storage;
using Microsoft.EntityFrameworkCore;

namespace DocVault.Infrastructure.Persistence.Repositories;

internal sealed class EfPendingBlobDeletionRepository : IPendingBlobDeletionRepository
{
  private readonly DocVaultDbContext _db;

  public EfPendingBlobDeletionRepository(DocVaultDbContext db) => _db = db;

  public async Task AddAsync(PendingBlobDeletion deletion, CancellationToken ct = default)
    => await _db.PendingBlobDeletions.AddAsync(deletion, ct);

  public Task<bool> ExistsByPathAsync(string storagePath, CancellationToken ct = default)
    => _db.PendingBlobDeletions.AnyAsync(p => p.StoragePath == storagePath, ct);

  public async Task<IReadOnlyList<PendingBlobDeletion>> GetPendingAsync(int maxAttempts, CancellationToken ct = default)
    => await _db.PendingBlobDeletions
        .Where(p => p.AttemptCount < maxAttempts)
        .OrderBy(p => p.CreatedAt)
        .ToListAsync(ct);

  public Task UpdateAsync(PendingBlobDeletion deletion, CancellationToken ct = default)
  {
    _db.PendingBlobDeletions.Update(deletion);
    return Task.CompletedTask;
  }

  public async Task DeleteAsync(Guid id, CancellationToken ct = default)
  {
    var entry = await _db.PendingBlobDeletions.FindAsync([id], ct);
    if (entry is not null)
      _db.PendingBlobDeletions.Remove(entry);
  }
}
