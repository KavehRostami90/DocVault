using DocVault.Domain.Storage;

namespace DocVault.Application.Abstractions.Persistence;

public interface IPendingBlobDeletionRepository
{
  Task AddAsync(PendingBlobDeletion deletion, CancellationToken ct = default);
  Task<bool> ExistsByPathAsync(string storagePath, CancellationToken ct = default);

  /// <summary>Returns entries whose <c>AttemptCount</c> is below <paramref name="maxAttempts"/>.</summary>
  Task<IReadOnlyList<PendingBlobDeletion>> GetPendingAsync(int maxAttempts, CancellationToken ct = default);

  Task UpdateAsync(PendingBlobDeletion deletion, CancellationToken ct = default);
  Task DeleteAsync(Guid id, CancellationToken ct = default);
}
