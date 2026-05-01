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
  /// Returns all jobs whose status is <see cref="ImportStatus.InProgress"/>.
  /// These are jobs that were dequeued and actively being processed when the
  /// process crashed — their queue row is already gone, so they need re-queuing
  /// on startup. Pending jobs are excluded because they still have a durable
  /// queue row and will be picked up automatically.
  /// </summary>
  Task<IReadOnlyList<ImportJob>> GetInProgressAsync(CancellationToken cancellationToken = default);

  /// <summary>
  /// Returns the most recently created <see cref="ImportJob"/> for the given document,
  /// or <c>null</c> if no job exists for that document.
  /// </summary>
  Task<ImportJob?> GetLatestByDocumentIdAsync(DocumentId documentId, CancellationToken cancellationToken = default);
}
