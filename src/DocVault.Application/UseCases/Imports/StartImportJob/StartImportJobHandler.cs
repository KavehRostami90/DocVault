using DocVault.Application.Abstractions.Persistence;
using DocVault.Application.Common.Results;
using DocVault.Domain.Imports;

namespace DocVault.Application.UseCases.Imports.StartImportJob;

public sealed class StartImportJobHandler
{
  private readonly IImportJobRepository _imports;

  public StartImportJobHandler(IImportJobRepository imports)
  {
    _imports = imports;
  }

  public async Task<Result<Guid>> HandleAsync(StartImportJobCommand command, CancellationToken cancellationToken = default)
  {
    var job = new ImportJob(Guid.NewGuid(), command.FileName, storagePath: string.Empty, contentType: string.Empty);
    await _imports.AddAsync(job, cancellationToken);
    return Result<Guid>.Success(job.Id);
  }
}
