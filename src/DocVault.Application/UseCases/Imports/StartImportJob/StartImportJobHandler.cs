using DocVault.Application.Abstractions.Persistence;
using DocVault.Application.Common.Results;
using DocVault.Domain.Imports;

namespace DocVault.Application.UseCases.Imports.StartImportJob;

/// <summary>
/// Handles creation of import jobs.
/// </summary>
public sealed class StartImportJobHandler
{
  private readonly IImportJobRepository _imports;

  /// <summary>
  /// Creates a new handler for starting import jobs.
  /// </summary>
  /// <param name="imports">Import job repository.</param>
  public StartImportJobHandler(IImportJobRepository imports)
  {
    _imports = imports;
  }

  /// <summary>
  /// Starts a new import job and returns its identifier.
  /// </summary>
  /// <param name="command">Start import command.</param>
  /// <param name="cancellationToken">Cancellation token.</param>
  /// <returns>Result containing the new job id.</returns>
  public async Task<Result<Guid>> HandleAsync(StartImportJobCommand command, CancellationToken cancellationToken = default)
  {
    var job = new ImportJob(Guid.NewGuid(), command.DocumentId, command.FileName, command.StoragePath, command.ContentType);
    await _imports.AddAsync(job, cancellationToken);
    return Result<Guid>.Success(job.Id);
  }
}
