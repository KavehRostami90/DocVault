using DocVault.Application.Abstractions.Cqrs;
using DocVault.Application.Abstractions.Persistence;
using DocVault.Application.Common.Results;
using DocVault.Domain.Extensions;
using DocVault.Domain.Imports;

namespace DocVault.Application.UseCases.Imports.GetImportStatus;

public sealed class GetImportStatusHandler : IQueryHandler<GetImportStatusQuery, Result<ImportJob>>
{
  private readonly IImportJobRepository _importJobsRepo;
  private readonly IDocumentRepository _documents;

  /// <summary>
  /// Creates a new handler for import status lookups.
  /// </summary>
  /// <param name="importJobsRepo">Import job repository.</param>
  /// <param name="documents">Document repository (used for ownership verification).</param>
  public GetImportStatusHandler(IImportJobRepository importJobsRepo, IDocumentRepository documents)
  {
    _importJobsRepo = importJobsRepo;
    _documents      = documents;
  }

  /// <summary>
  /// Retrieves the status of an import job by id, enforcing ownership.
  /// Non-admin callers may only see jobs whose linked document they own.
  /// </summary>
  /// <param name="query">Query containing the job id and caller identity.</param>
  /// <param name="cancellationToken">Cancellation token.</param>
  /// <returns>Result containing the job when found and accessible.</returns>
  public async Task<Result<ImportJob>> HandleAsync(GetImportStatusQuery query, CancellationToken cancellationToken = default)
  {
    var job = await _importJobsRepo.GetAsync(query.Id, cancellationToken).ConfigureAwait(false);
    if (job is null)
      return Result<ImportJob>.Failure(Errors.NotFound);

    if (!query.IsAdmin)
    {
      var doc = await _documents.GetAsync(job.DocumentId, cancellationToken).ConfigureAwait(false);
      if (doc is null || doc.OwnerId != query.CallerId)
        return Result<ImportJob>.Failure(Errors.NotFound);
    }

    _ = job.IsTerminal();
    return Result<ImportJob>.Success(job);
  }
}
