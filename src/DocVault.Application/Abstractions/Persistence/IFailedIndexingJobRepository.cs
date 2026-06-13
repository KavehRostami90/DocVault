using DocVault.Domain.Imports;

namespace DocVault.Application.Abstractions.Persistence;

public interface IFailedIndexingJobRepository
{
    Task<FailedIndexingJob?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<FailedIndexingJob?> GetByJobIdAsync(Guid jobId, CancellationToken cancellationToken = default);

    /// <summary>Returns non-exhausted entries whose NextRetryAt is in the past and not currently in-flight (NextRetryAt is not null).</summary>
    Task<IReadOnlyList<FailedIndexingJob>> GetDueForRetryAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FailedIndexingJob>> GetAllAsync(int page, int pageSize, CancellationToken cancellationToken = default);
    Task<int> CountAsync(CancellationToken cancellationToken = default);

    Task AddAsync(FailedIndexingJob entry, CancellationToken cancellationToken = default);
    Task UpdateAsync(FailedIndexingJob entry, CancellationToken cancellationToken = default);
    Task DeleteAsync(FailedIndexingJob entry, CancellationToken cancellationToken = default);
}
