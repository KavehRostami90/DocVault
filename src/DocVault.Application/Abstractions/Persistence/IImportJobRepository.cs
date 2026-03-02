using DocVault.Domain.Imports;

namespace DocVault.Application.Abstractions.Persistence;

public interface IImportJobRepository
{
  Task<ImportJob?> GetAsync(Guid id, CancellationToken cancellationToken = default);
  Task AddAsync(ImportJob job, CancellationToken cancellationToken = default);
  Task UpdateAsync(ImportJob job, CancellationToken cancellationToken = default);
  /// <summary>
  /// Returns all jobs whose status is <see cref="ImportStatus.Pending"/> or
  /// <see cref="ImportStatus.InProgress"/> so they can be re-queued after a
  /// process restart.
  /// </summary>
  Task<IReadOnlyList<ImportJob>> GetPendingAsync(CancellationToken cancellationToken = default);
}
