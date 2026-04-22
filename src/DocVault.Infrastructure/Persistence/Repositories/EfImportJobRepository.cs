using DocVault.Application.Abstractions.Persistence;
using DocVault.Domain.Documents;
using DocVault.Domain.Imports;
using Microsoft.EntityFrameworkCore;

namespace DocVault.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IImportJobRepository"/>.
/// Manages persistence and querying of <see cref="ImportJob"/> aggregates.
/// </summary>
public class EfImportJobRepository : IImportJobRepository
{
  private readonly DocVaultDbContext _db;

  /// <summary>Initialises the repository with the scoped database context.</summary>
  /// <param name="db">The EF Core database context for this request scope.</param>
  public EfImportJobRepository(DocVaultDbContext db)
  {
    _db = db;
  }

  /// <summary>Persists a new <see cref="ImportJob"/> and saves changes.</summary>
  /// <param name="job">The import job to add.</param>
  /// <param name="cancellationToken">Cancellation token.</param>
  public async Task AddAsync(ImportJob job, CancellationToken cancellationToken = default)
    => await _db.ImportJobs.AddAsync(job, cancellationToken);

  /// <summary>Retrieves a single <see cref="ImportJob"/> by its identifier.</summary>
  /// <param name="id">The unique job identifier.</param>
  /// <param name="cancellationToken">Cancellation token.</param>
  /// <returns>The matching job, or <c>null</c> if not found.</returns>
  public Task<ImportJob?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    => _db.ImportJobs.FirstOrDefaultAsync(j => j.Id == id, cancellationToken);

  /// <summary>
  /// Returns all jobs whose status is <see cref="ImportStatus.Pending"/> or
  /// <see cref="ImportStatus.InProgress"/>. Used by the background worker on
  /// startup to recover from process crashes.
  /// </summary>
  /// <param name="cancellationToken">Cancellation token.</param>
  /// <returns>A read-only list of jobs requiring processing.</returns>
  public async Task<IReadOnlyList<ImportJob>> GetPendingAsync(CancellationToken cancellationToken = default)
    => await _db.ImportJobs
      .Where(j => j.Status == ImportStatus.Pending || j.Status == ImportStatus.InProgress)
      .ToListAsync(cancellationToken);

  /// <summary>Updates an existing <see cref="ImportJob"/> and saves changes.</summary>
  /// <param name="job">The job aggregate with updated state.</param>
  /// <param name="cancellationToken">Cancellation token.</param>
  public Task UpdateAsync(ImportJob job, CancellationToken cancellationToken = default)
  {
    _db.ImportJobs.Update(job);
    return Task.CompletedTask;
  }

    /// <summary>Retrieves the most recent <see cref="ImportJob"/> for a given document, if any exist.</summary>
    /// <param name="documentId">The document identifier to search by.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The latest import job for the document, or <c>null</c> if no jobs exist.</returns>
  public Task<ImportJob?> GetLatestByDocumentIdAsync(DocumentId documentId, CancellationToken cancellationToken = default)
    => _db.ImportJobs
      .Where(j => j.DocumentId == documentId)
      .OrderByDescending(j => j.StartedAt)
      .FirstOrDefaultAsync(cancellationToken);
}
