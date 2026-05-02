using DocVault.Application.Background.Queue;

namespace DocVault.Application.Abstractions.Persistence;

/// <summary>
/// Stages a new work item in the indexing queue within the current unit-of-work scope.
/// Implementations must NOT flush changes themselves — the surrounding transaction commits them.
/// </summary>
public interface IIndexingQueueRepository
{
  Task AddAsync(IndexingWorkItem item, CancellationToken cancellationToken = default);
}
