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
    return doc is null ? Result<Document>.Failure(Errors.NotFound) : Result<Document>.Success(doc);
  }
}
