using DocVault.Application.Abstractions.Cqrs;
using DocVault.Application.Abstractions.Persistence;
using DocVault.Application.Common.Results;
using DocVault.Domain.Imports;

namespace DocVault.Application.UseCases.Imports.StartImportJob;

public sealed class StartImportJobHandler : ICommandHandler<StartImportJobCommand, Result<Guid>>
{
  private readonly IImportJobRepository _imports;
  private readonly IDocumentRepository  _documents;
  private readonly IUnitOfWork _unitOfWork;

  public StartImportJobHandler(IImportJobRepository imports, IDocumentRepository documents, IUnitOfWork unitOfWork)
  {
    _imports   = imports;
    _documents = documents;
    _unitOfWork = unitOfWork;
  }

  public async Task<Result<Guid>> HandleAsync(StartImportJobCommand command, CancellationToken cancellationToken = default)
  {
    if (!command.IsAdmin)
    {
      var doc = await _documents.GetAsync(command.DocumentId, cancellationToken);
      if (doc is null || doc.OwnerId != command.CallerId)
        return Result<Guid>.Failure(Errors.NotFound);
    }

    var job = new ImportJob(Guid.NewGuid(), command.DocumentId, command.FileName, command.StoragePath, command.ContentType);
    await _imports.AddAsync(job, cancellationToken);
    await _unitOfWork.SaveChangesAsync(cancellationToken);

    return Result<Guid>.Success(job.Id);
  }
}
