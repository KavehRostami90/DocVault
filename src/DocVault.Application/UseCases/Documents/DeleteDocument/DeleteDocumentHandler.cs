using DocVault.Application.Abstractions.Cqrs;
using DocVault.Application.Abstractions.Persistence;
using DocVault.Application.Abstractions.Storage;
using DocVault.Application.Common.Results;
using DocVault.Domain.Extensions;
using DocVault.Domain.Storage;

namespace DocVault.Application.UseCases.Documents.DeleteDocument;

public sealed class DeleteDocumentHandler : ICommandHandler<DeleteDocumentCommand, Result>
{
  private readonly IDocumentRepository _documents;
  private readonly IFileStorage _storage;
  private readonly IUnitOfWork _unitOfWork;
  private readonly IPendingBlobDeletionRepository _pendingDeletions;

  public DeleteDocumentHandler(
    IDocumentRepository documents,
    IFileStorage storage,
    IUnitOfWork unitOfWork,
    IPendingBlobDeletionRepository pendingDeletions)
  {
    _documents        = documents;
    _storage          = storage;
    _unitOfWork       = unitOfWork;
    _pendingDeletions = pendingDeletions;
  }

  public async Task<Result> HandleAsync(DeleteDocumentCommand command, CancellationToken cancellationToken = default)
  {
    var doc = await _documents.GetAsync(command.Id, cancellationToken);
    if (doc is null)
      return Result.Failure(Errors.NotFound);

    if (!command.IsAdmin && doc.OwnerId != command.CallerId)
      return Result.Failure(Errors.NotFound);

    if (doc.IsPending())
      return Result.Failure(Errors.Conflict);

    var storagePath = $"{doc.Id.Value}.bin";

    await _documents.DeleteAsync(doc, cancellationToken);
    await _unitOfWork.SaveChangesAsync(cancellationToken);

    // Attempt immediate blob deletion; on failure queue for background retry so the
    // document record deletion is never rolled back on storage errors.
    try
    {
      await _storage.DeleteAsync(storagePath, cancellationToken);
    }
    catch
    {
      try
      {
        if (!await _pendingDeletions.ExistsByPathAsync(storagePath, cancellationToken))
        {
          await _pendingDeletions.AddAsync(new PendingBlobDeletion(Guid.NewGuid(), storagePath), cancellationToken);
          await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
      }
      catch { /* best-effort: BlobCleanupWorker will catch orphans during reconciliation */ }
    }

    return Result.Success();
  }
}
