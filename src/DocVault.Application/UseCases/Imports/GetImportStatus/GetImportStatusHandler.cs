using DocVault.Application.Abstractions.Persistence;
using DocVault.Application.Common.Results;
using DocVault.Domain.Extensions;
using DocVault.Domain.Imports;

namespace DocVault.Application.UseCases.Imports.GetImportStatus;

/// <summary>
/// Handles retrieval of import job status.
/// </summary>
public sealed class GetImportStatusHandler
{
  private readonly IImportJobRepository _importJobsRepo;

  /// <summary>
  /// Creates a new handler for import status lookups.
  /// </summary>
  /// <param name="importJobsRepo">Import job repository.</param>
  public GetImportStatusHandler(IImportJobRepository importJobsRepo)
  {
    _importJobsRepo = importJobsRepo;
  }

  /// <summary>
  /// Retrieves the status of an import job by id.
  /// </summary>
  /// <param name="query">Query containing the job id.</param>
  /// <param name="cancellationToken">Cancellation token.</param>
  /// <returns>Result containing the job when found.</returns>
  public async Task<Result<ImportJob>> HandleAsync(GetImportStatusQuery query, CancellationToken cancellationToken = default)
  {
    var job = await _importJobsRepo.GetAsync(query.Id, cancellationToken).ConfigureAwait(false);
    if (job is null)
    {
      return Result<ImportJob>.Failure(Errors.NotFound);
    }

    // Using extension to flag terminal state could help callers short-circuit.
    _ = job.IsTerminal();
    return Result<ImportJob>.Success(job);
  }
}
