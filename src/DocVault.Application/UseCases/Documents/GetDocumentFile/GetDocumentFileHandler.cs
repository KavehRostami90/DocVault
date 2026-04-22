using DocVault.Application.Abstractions.Cqrs;
using DocVault.Application.Abstractions.Persistence;
using DocVault.Application.Common.Results;

namespace DocVault.Application.UseCases.Documents.GetDocumentFile;

public sealed class GetDocumentFileHandler : IQueryHandler<GetDocumentFileQuery, Result<DocumentFileReference>>
{
  private readonly IDocumentRepository _documents;
  private readonly IImportJobRepository _imports;

  public GetDocumentFileHandler(
    IDocumentRepository documents,
    IImportJobRepository imports)
  {
    _documents = documents;
    _imports = imports;
  }

  public async Task<Result<DocumentFileReference>> HandleAsync(
    GetDocumentFileQuery query,
    CancellationToken cancellationToken = default)
  {
    var doc = await _documents.GetAsync(query.Id, cancellationToken);
    if (doc is null)
      return Result<DocumentFileReference>.Failure(Errors.NotFound);

    if (!query.IsAdmin && doc.OwnerId != query.CallerId)
      return Result<DocumentFileReference>.Failure(Errors.NotFound);

    var importJob = await _imports.GetLatestByDocumentIdAsync(doc.Id, cancellationToken);
    if (importJob is null)
      return Result<DocumentFileReference>.Failure(Errors.NotFound);

    return Result<DocumentFileReference>.Success(
      new DocumentFileReference(doc.FileName, doc.ContentType, importJob.StoragePath));
  }
}
