using DocVault.Application.Abstractions.Persistence;
using DocVault.Domain.Imports;
using Microsoft.EntityFrameworkCore;

namespace DocVault.Infrastructure.Persistence.Repositories;

public sealed class EfFailedIndexingJobRepository : IFailedIndexingJobRepository
{
    private readonly DocVaultDbContext _db;

    public EfFailedIndexingJobRepository(DocVaultDbContext db) => _db = db;

    public Task<FailedIndexingJob?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _db.FailedIndexingJobs.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public Task<FailedIndexingJob?> GetByJobIdAsync(Guid jobId, CancellationToken cancellationToken = default)
        => _db.FailedIndexingJobs.FirstOrDefaultAsync(e => e.JobId == jobId, cancellationToken);

    public async Task<IReadOnlyList<FailedIndexingJob>> GetDueForRetryAsync(CancellationToken cancellationToken = default)
        => await _db.FailedIndexingJobs
            .Where(e => !e.IsExhausted && e.NextRetryAt != null && e.NextRetryAt <= DateTimeOffset.UtcNow)
            .OrderBy(e => e.NextRetryAt)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<FailedIndexingJob>> GetAllAsync(int page, int pageSize, CancellationToken cancellationToken = default)
        => await _db.FailedIndexingJobs
            .OrderByDescending(e => e.LastFailedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

    public Task<int> CountAsync(CancellationToken cancellationToken = default)
        => _db.FailedIndexingJobs.CountAsync(cancellationToken);

    public async Task AddAsync(FailedIndexingJob entry, CancellationToken cancellationToken = default)
        => await _db.FailedIndexingJobs.AddAsync(entry, cancellationToken);

    public Task UpdateAsync(FailedIndexingJob entry, CancellationToken cancellationToken = default)
    {
        _db.FailedIndexingJobs.Update(entry);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(FailedIndexingJob entry, CancellationToken cancellationToken = default)
    {
        _db.FailedIndexingJobs.Remove(entry);
        return Task.CompletedTask;
    }
}
