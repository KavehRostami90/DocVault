using System.Security.Cryptography;
using DocVault.Application.Abstractions.Cqrs;
using DocVault.Application.Abstractions.Persistence;
using DocVault.Application.Abstractions.Storage;
using DocVault.Application.Background.Queue;
using DocVault.Application.Common.Results;
using DocVault.Domain.Documents;
using DocVault.Domain.Documents.ValueObjects;
using DocVault.Domain.Imports;

namespace DocVault.Application.UseCases.Documents.ImportDocument;

public sealed class ImportDocumentHandler : ICommandHandler<ImportDocumentCommand, Result<DocumentId>>
{
  private readonly IDocumentRepository _documents;
  private readonly IImportJobRepository _imports;
  private readonly IFileStorage _storage;
  private readonly IIndexingQueueRepository _queue;
  private readonly IUnitOfWork _unitOfWork;

  public ImportDocumentHandler(
    IDocumentRepository documents,
    IImportJobRepository imports,
    IFileStorage storage,
    IIndexingQueueRepository queue,
    IUnitOfWork unitOfWork)
  {
    _documents  = documents;
    _imports    = imports;
    _storage    = storage;
    _queue      = queue;
    _unitOfWork = unitOfWork;
  }

  public async Task<Result<DocumentId>> HandleAsync(ImportDocumentCommand command, CancellationToken cancellationToken = default)
  {
    var documentId = DocumentId.New();

    using var buffer = new MemoryStream();
    await command.Content.CopyToAsync(buffer, cancellationToken);

    var hash        = ComputeHash(buffer);
    var storagePath = $"{documentId.Value}.bin";

    buffer.Position = 0;
    await _storage.WriteAsync(storagePath, buffer, cancellationToken);

    var tags     = command.Tags.Select(t => new Tag(Guid.NewGuid(), t));
    var document = new Document(documentId, command.Title, command.FileName, command.ContentType, command.Size, hash, command.OwnerId);
    document.ReplaceTags(tags);
    document.MarkImported();

    var job     = new ImportJob(Guid.NewGuid(), documentId, command.FileName, storagePath, command.ContentType);
    var workItem = new IndexingWorkItem(job.Id, storagePath, command.ContentType);

    // Persist the document, import job, and queue entry atomically.
    // The queue row is committed in the same transaction so a process crash
    // between these writes can never leave a job without a queue entry.
    await _unitOfWork.ExecuteInTransactionAsync(async ct =>
    {
      await _documents.AddAsync(document, ct);
      await _imports.AddAsync(job, ct);
      await _queue.AddAsync(workItem, ct);
    }, cancellationToken);

    return Result<DocumentId>.Success(documentId);
  }

  private static FileHash ComputeHash(MemoryStream buffer)
  {
    var hashBytes = SHA256.HashData(buffer.GetBuffer().AsSpan(0, (int)buffer.Length));
    return FileHash.FromBytes(hashBytes);
  }
}
