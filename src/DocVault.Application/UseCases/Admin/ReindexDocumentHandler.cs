using DocVault.Application.Abstractions.Cqrs;
using DocVault.Application.Abstractions.Persistence;
using DocVault.Application.Background.Queue;
using DocVault.Application.Common.Results;
using DocVault.Domain.Imports;

namespace DocVault.Application.UseCases.Admin;

public sealed class ReindexDocumentHandler : ICommandHandler<ReindexDocumentCommand, Result>
{
  private readonly IDocumentRepository _documents;
  private readonly IImportJobRepository _imports;
  private readonly IIndexingQueueRepository _queue;
  private readonly IUnitOfWork _unitOfWork;

  public ReindexDocumentHandler(
    IDocumentRepository documents,
    IImportJobRepository imports,
    IIndexingQueueRepository queue,
    IUnitOfWork unitOfWork)
  {
    _documents  = documents;
    _imports    = imports;
    _queue      = queue;
    _unitOfWork = unitOfWork;
  }

  public async Task<Result> HandleAsync(ReindexDocumentCommand command, CancellationToken cancellationToken = default)
  {
    var doc = await _documents.GetAsync(command.DocumentId, cancellationToken);
    if (doc is null)
      return Result.Failure(Errors.NotFound);

    var storagePath = $"{doc.Id.Value}.bin";
    doc.PrepareForReindex();

    var job      = new ImportJob(Guid.NewGuid(), doc.Id, doc.FileName, storagePath, doc.ContentType);
    var workItem = new IndexingWorkItem(job.Id, storagePath, doc.ContentType);

    await _unitOfWork.ExecuteInTransactionAsync(async ct =>
    {
      await _documents.UpdateAsync(doc, ct);
      await _imports.AddAsync(job, ct);
      await _queue.AddAsync(workItem, ct);
    }, cancellationToken);

    return Result.Success();
  }
}
