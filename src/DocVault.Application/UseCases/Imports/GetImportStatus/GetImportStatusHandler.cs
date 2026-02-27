using DocVault.Application.Abstractions.Persistence;
using DocVault.Application.Common.Results;
using DocVault.Domain.Extensions;
using DocVault.Domain.Imports;

namespace DocVault.Application.UseCases.Imports.GetImportStatus;

public sealed class GetImportStatusHandler
{
  private readonly IImportJobRepository _imports;

  public GetImportStatusHandler(IImportJobRepository imports)
  {
    _imports = imports;
  }

  public async Task<Result<ImportJob>> HandleAsync(GetImportStatusQuery query, CancellationToken cancellationToken = default)
  {
    var job = await _imports.GetAsync(query.Id, cancellationToken);
    if (job is null)
    {
      return Result<ImportJob>.Failure(Errors.NotFound);
    }

    // Using extension to flag terminal state could help callers short-circuit.
    _ = job.IsTerminal();
    return Result<ImportJob>.Success(job);
  }
}
