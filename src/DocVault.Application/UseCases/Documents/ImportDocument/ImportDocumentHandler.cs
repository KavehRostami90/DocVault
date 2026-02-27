using DocVault.Application.Abstractions.Persistence;
using DocVault.Application.Abstractions.Storage;
using DocVault.Application.Common.Results;
using DocVault.Domain.Documents;
using DocVault.Domain.Documents.ValueObjects;
using DocVault.Domain.Imports;

namespace DocVault.Application.UseCases.Documents.ImportDocument;

public sealed class ImportDocumentHandler
{
  private readonly IDocumentRepository _documents;
  private readonly IImportJobRepository _imports;
  private readonly IFileStorage _storage;

  public ImportDocumentHandler(IDocumentRepository documents, IImportJobRepository imports, IFileStorage storage)
  {
    _documents = documents;
    _imports = imports;
    _storage = storage;
  }

  public async Task<Result<DocumentId>> HandleAsync(ImportDocumentCommand command, CancellationToken cancellationToken = default)
  {
    var documentId = DocumentId.New();
    var job = new ImportJob(Guid.NewGuid(), command.FileName);
    await _imports.AddAsync(job, cancellationToken);

    var path = $"{documentId.Value}.bin";
    await _storage.WriteAsync(path, command.Content, cancellationToken);

    var document = new Document(documentId, command.FileName, command.FileName, string.Empty, 0, new FileHash(string.Empty));
    document.MarkImported();
    await _documents.AddAsync(document, cancellationToken);

    return Result<DocumentId>.Success(documentId);
  }
}
