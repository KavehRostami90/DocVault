using DocVault.Application.Abstractions.Cqrs;
using DocVault.Application.Abstractions.Persistence;
using DocVault.Application.Abstractions.Storage;
using DocVault.Application.Common.Results;
using DocVault.Domain.Extensions;

namespace DocVault.Application.UseCases.Documents.DeleteDocument;

public sealed class DeleteDocumentHandler : ICommandHandler<DeleteDocumentCommand, Result>
{
  private readonly IDocumentRepository _documents;
  private readonly IFileStorage _storage;
  private readonly IUnitOfWork _unitOfWork;

  public DeleteDocumentHandler(IDocumentRepository documents, IFileStorage storage, IUnitOfWork unitOfWork)
  {
    _documents  = documents;
    _storage    = storage;
    _unitOfWork = unitOfWork;
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

    await _documents.DeleteAsync(doc, cancellationToken);
    await _unitOfWork.SaveChangesAsync(cancellationToken);

    // Best-effort: the DB record is gone — clean up the binary blob if possible.
    try
    {
      await _storage.DeleteAsync($"{doc.Id.Value}.bin", cancellationToken);
    }
    catch { /* orphaned blob is preferable to failing a completed delete */ }

    return Result.Success();
  }
}
