using DocVault.Application.Abstractions.Persistence;
using DocVault.Application.Background.Queue;
using DocVault.Application.Common.Results;
using DocVault.Domain.Imports;

namespace DocVault.Application.UseCases.Admin;

/// <summary>
/// Handles <see cref="ReindexDocumentCommand"/> by resetting a document to the
/// <c>Imported</c> status, creating a new <see cref="ImportJob"/>, and enqueuing
/// it for the background indexing pipeline.
/// </summary>
public sealed class ReindexDocumentHandler
{
  private readonly IDocumentRepository _documents;
  private readonly IImportJobRepository _imports;
  private readonly IWorkQueue<IndexingWorkItem> _queue;

  public ReindexDocumentHandler(
    IDocumentRepository documents,
    IImportJobRepository imports,
    IWorkQueue<IndexingWorkItem> queue)
  {
    _documents = documents;
    _imports   = imports;
    _queue     = queue;
  }

  /// <summary>
  /// Re-queues the specified document for the full ingestion pipeline.
  /// </summary>
  /// <param name="command">Command carrying the ID of the document to reindex.</param>
  /// <param name="cancellationToken">Cancellation token.</param>
  /// <returns>
  /// <see cref="Result.Success"/> when the document was found and enqueued;
  /// <see cref="Errors.NotFound"/> when no document with the given ID exists.
  /// </returns>
  public async Task<Result> HandleAsync(ReindexDocumentCommand command, CancellationToken cancellationToken = default)
  {
    var doc = await _documents.GetAsync(command.DocumentId, cancellationToken);
    if (doc is null)
      return Result.Failure(Errors.NotFound);

    // Derive the storage path from the document ID (matches ImportDocumentHandler convention).
    var storagePath = $"{doc.Id.Value}.bin";

    doc.MarkImported();
    await _documents.UpdateAsync(doc, cancellationToken);

    var job = new ImportJob(Guid.NewGuid(), doc.Id, doc.FileName, storagePath, doc.ContentType);
    await _imports.AddAsync(job, cancellationToken);

    _queue.Enqueue(new IndexingWorkItem(job.Id, storagePath, doc.ContentType));

    return Result.Success();
  }
}
