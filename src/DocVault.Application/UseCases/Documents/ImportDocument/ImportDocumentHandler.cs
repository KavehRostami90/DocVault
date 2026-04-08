using System.Security.Cryptography;
using DocVault.Application.Abstractions.Persistence;
using DocVault.Application.Abstractions.Storage;
using DocVault.Application.Background.Queue;
using DocVault.Application.Common.Results;
using DocVault.Domain.Documents;
using DocVault.Domain.Documents.ValueObjects;
using DocVault.Domain.Imports;

namespace DocVault.Application.UseCases.Documents.ImportDocument;

/// <summary>
/// Handles importing a document, persisting metadata, and enqueuing indexing work.
/// </summary>
public sealed class ImportDocumentHandler
{
  private readonly IDocumentRepository _documents;
  private readonly IImportJobRepository _imports;
  private readonly IFileStorage _storage;
  private readonly IWorkQueue<IndexingWorkItem> _queue;
  private readonly IUnitOfWork _unitOfWork;

  /// <summary>
  /// Initializes the handler with required repositories and services.
  /// </summary>
  public ImportDocumentHandler(
    IDocumentRepository documents,
    IImportJobRepository imports,
    IFileStorage storage,
    IWorkQueue<IndexingWorkItem> queue,
    IUnitOfWork unitOfWork)
  {
    _documents  = documents;
    _imports    = imports;
    _storage    = storage;
    _queue      = queue;
    _unitOfWork = unitOfWork;
  }

  /// <summary>
  /// Imports a document and returns its identifier.
  /// </summary>
  /// <param name="command">Import command payload.</param>
  /// <param name="cancellationToken">Cancellation token.</param>
  /// <returns>Result containing the document identifier on success.</returns>
  public async Task<Result<DocumentId>> HandleAsync(ImportDocumentCommand command, CancellationToken cancellationToken = default)
  {
    var documentId = DocumentId.New();

    // Buffer the entire upload so we can (a) compute hash and (b) write to storage
    // without reading the HTTP stream twice.
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

    // Persist document and import job in a single transaction so the system
    // never ends up with a document without a corresponding job (or vice-versa).
    var job = new ImportJob(Guid.NewGuid(), documentId, command.FileName, storagePath, command.ContentType);
    await _unitOfWork.ExecuteInTransactionAsync(async ct =>
    {
      await _documents.AddAsync(document, ct);
      await _imports.AddAsync(job, ct);
    }, cancellationToken);

    // Hand off to the background indexing pipeline.
    _queue.Enqueue(new IndexingWorkItem(job.Id, storagePath, command.ContentType));

    return Result<DocumentId>.Success(documentId);
  }

  /// <summary>Computes a SHA-256 hash of the buffered content without re-reading.</summary>
  private static FileHash ComputeHash(MemoryStream buffer)
  {
    var hashBytes = SHA256.HashData(buffer.GetBuffer().AsSpan(0, (int)buffer.Length));
    return FileHash.FromBytes(hashBytes);
  }
}
