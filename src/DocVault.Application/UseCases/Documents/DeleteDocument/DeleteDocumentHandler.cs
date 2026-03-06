using DocVault.Application.Abstractions.Persistence;
using DocVault.Application.Common.Results;
using DocVault.Domain.Extensions;

namespace DocVault.Application.UseCases.Documents.DeleteDocument;

/// <summary>
/// Handles deletion of documents.
/// </summary>
public sealed class DeleteDocumentHandler
{
  private readonly IDocumentRepository _documents;

  /// <summary>
  /// Creates a new handler for deleting documents.
  /// </summary>
  /// <param name="documents">Document repository.</param>
  public DeleteDocumentHandler(IDocumentRepository documents)
  {
    _documents = documents;
  }

  /// <summary>
  /// Deletes the specified document if allowed.
  /// </summary>
  /// <param name="command">Delete command containing the document id.</param>
  /// <param name="cancellationToken">Cancellation token.</param>
  /// <returns>Result indicating success or failure.</returns>
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
