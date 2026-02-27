using DocVault.Application.Abstractions.Persistence;
using DocVault.Application.Common.Results;
using DocVault.Domain.Extensions;

namespace DocVault.Application.UseCases.Documents.DeleteDocument;

public sealed class DeleteDocumentHandler
{
  private readonly IDocumentRepository _documents;

  public DeleteDocumentHandler(IDocumentRepository documents)
  {
    _documents = documents;
  }

  public async Task<Result> HandleAsync(DeleteDocumentCommand command, CancellationToken cancellationToken = default)
  {
    var doc = await _documents.GetAsync(command.Id, cancellationToken);
    if (doc is null)
    {
      return Result.Failure(Errors.NotFound);
    }

    if (doc.IsPending())
    {
      return Result.Failure(Errors.Conflict);
    }

    await _documents.DeleteAsync(doc, cancellationToken);
    return Result.Success();
  }
}
