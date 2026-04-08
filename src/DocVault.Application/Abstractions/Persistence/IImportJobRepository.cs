using DocVault.Domain.Documents;
using DocVault.Domain.Imports;

namespace DocVault.Application.Abstractions.Persistence;

public interface IImportJobRepository
{
  /// <summary>Returns the import job with the given <paramref name="id"/>, or <c>null</c> if not found.</summary>
  Task<ImportJob?> GetAsync(Guid id, CancellationToken cancellationToken = default);

  /// <summary>Persists a new import job to the store.</summary>
  Task AddAsync(ImportJob job, CancellationToken cancellationToken = default);

  /// <summary>Saves changes to an existing import job.</summary>
  Task UpdateAsync(ImportJob job, CancellationToken cancellationToken = default);

  /// <summary>
  /// Returns all jobs whose status is <see cref="ImportStatus.Pending"/> or
  /// <see cref="ImportStatus.InProgress"/> so they can be re-queued after a
  /// process restart.
  /// </summary>
  Task<IReadOnlyList<ImportJob>> GetPendingAsync(CancellationToken cancellationToken = default);

  /// <summary>
  /// Returns the most recently created <see cref="ImportJob"/> for the given document,
  /// or <c>null</c> if no job exists for that document.
  /// </summary>
  Task<ImportJob?> GetLatestByDocumentIdAsync(DocumentId documentId, CancellationToken cancellationToken = default);
}
