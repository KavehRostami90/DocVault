using DocVault.Application.Abstractions.Cqrs;
using DocVault.Application.Abstractions.Persistence;
using DocVault.Application.Background;
using DocVault.Application.Background.Queue;
using DocVault.Application.Common.Results;
using DocVault.Domain.Documents;

namespace DocVault.Application.UseCases.Admin.DeadLetterQueue;

public sealed class RetryDeadLetterJobHandler : ICommandHandler<RetryDeadLetterJobCommand, Result>
{
  private readonly IFailedIndexingJobRepository _dlqRepo;
  private readonly IImportJobRepository         _importJobRepo;
  private readonly IDocumentRepository          _documentRepo;
  private readonly IUnitOfWork                  _unitOfWork;
  private readonly IWorkQueue<IndexingWorkItem>  _queue;

  public RetryDeadLetterJobHandler(
    IFailedIndexingJobRepository dlqRepo,
    IImportJobRepository importJobRepo,
    IDocumentRepository documentRepo,
    IUnitOfWork unitOfWork,
    IWorkQueue<IndexingWorkItem> queue)
  {
    _dlqRepo       = dlqRepo;
    _importJobRepo = importJobRepo;
    _documentRepo  = documentRepo;
    _unitOfWork    = unitOfWork;
    _queue         = queue;
  }

  public async Task<Result> HandleAsync(RetryDeadLetterJobCommand command, CancellationToken cancellationToken = default)
  {
    var entry = await _dlqRepo.GetByIdAsync(command.EntryId, cancellationToken);
    if (entry is null)
      return Result.Failure("Dead-letter entry not found.");

    var job = await _importJobRepo.GetAsync(entry.JobId, cancellationToken);
    if (job is null)
      return Result.Failure("Associated import job not found.");

    entry.ScheduleImmediateRetry();
    job.MarkPendingRetry();

    await _dlqRepo.UpdateAsync(entry, cancellationToken);
    await _importJobRepo.UpdateAsync(job, cancellationToken);

    // If the document was marked Failed (exhausted DLQ entry), reset it to Imported
    // so the indexing worker can call MarkIndexed() after a successful run.
    // PrepareForReindex() is a no-op when the document is already Imported.
    var document = await _documentRepo.GetAsync(job.DocumentId, cancellationToken);
    if (document is not null && document.Status == DocumentStatus.Failed)
    {
      document.PrepareForReindex();
      await _documentRepo.UpdateAsync(document, cancellationToken);
    }

    await _unitOfWork.SaveChangesAsync(cancellationToken);

    // Enqueue before persisting MarkRetrying. If the process dies after the save
    // above but before the enqueue, NextRetryAt is still set and the polling worker
    // will re-pick the entry — far better than stranding it with NextRetryAt = null
    // and no queue item, which GetDueForRetryAsync would never return.
    _queue.Enqueue(new IndexingWorkItem(job.Id, entry.StoragePath, entry.ContentType));

    // Clear NextRetryAt so the polling worker doesn't double-pick while in-flight.
    entry.MarkRetrying();
    await _dlqRepo.UpdateAsync(entry, cancellationToken);
    await _unitOfWork.SaveChangesAsync(cancellationToken);

    return Result.Success();
  }
}
