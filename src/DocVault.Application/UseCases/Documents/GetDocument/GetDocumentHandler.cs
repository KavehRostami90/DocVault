using DocVault.Application.Abstractions.Persistence;
using DocVault.Application.Common.Results;
using DocVault.Domain.Documents;

namespace DocVault.Application.UseCases.Documents.GetDocument;

public sealed class GetDocumentHandler
{
  private readonly IDocumentRepository _documents;

  public GetDocumentHandler(IDocumentRepository documents)
  {
    _documents = documents;
  }

  public async Task<Result<Document>> HandleAsync(GetDocumentQuery query, CancellationToken cancellationToken = default)
  {
    var doc = await _documents.GetAsync(query.Id, cancellationToken);
    if (doc is null)
      return Result<Document>.Failure(Errors.NotFound);

    if (!query.IsAdmin && doc.OwnerId != query.CallerId)
      return Result<Document>.Failure(Errors.NotFound);

    return Result<Document>.Success(doc);
  }
}
