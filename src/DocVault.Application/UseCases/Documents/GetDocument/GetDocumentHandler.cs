using DocVault.Application.Abstractions.Persistence;
using DocVault.Application.Common.Results;
using DocVault.Domain.Documents;

namespace DocVault.Application.UseCases.Documents.GetDocument;

/// <summary>
/// Handles retrieval of a single document by identifier.
/// </summary>
public sealed class GetDocumentHandler
{
  private readonly IDocumentRepository _documents;

  /// <summary>
  /// Creates a new handler for document retrieval.
  /// </summary>
  /// <param name="documents">Document repository.</param>
  public GetDocumentHandler(IDocumentRepository documents)
  {
    _documents = documents;
  }

  /// <summary>
  /// Retrieves a document by id.
  /// </summary>
  /// <param name="query">Query containing the document id.</param>
  /// <param name="cancellationToken">Cancellation token.</param>
  /// <returns>Result with the document when found.</returns>
  public async Task<Result<Document>> HandleAsync(GetDocumentQuery query, CancellationToken cancellationToken = default)
  {
    var doc = await _documents.GetAsync(query.Id, cancellationToken);
    return doc is null ? Result<Document>.Failure(Errors.NotFound) : Result<Document>.Success(doc);
  }
}
