using DocVault.Application.Abstractions.Cqrs;
using DocVault.Application.Abstractions.Persistence;
using DocVault.Application.Background;
using DocVault.Application.Background.Queue;
using DocVault.Application.Common.Results;

namespace DocVault.Application.UseCases.Admin.DeadLetterQueue;

public sealed class RetryDeadLetterJobHandler : ICommandHandler<RetryDeadLetterJobCommand, Result>
{
  private readonly IFailedIndexingJobRepository _dlqRepo;
  private readonly IImportJobRepository         _importJobRepo;
  private readonly IUnitOfWork                  _unitOfWork;
  private readonly IWorkQueue<IndexingWorkItem>  _queue;

  public RetryDeadLetterJobHandler(
    IFailedIndexingJobRepository dlqRepo,
    IImportJobRepository importJobRepo,
    IUnitOfWork unitOfWork,
    IWorkQueue<IndexingWorkItem> queue)
  {
    _dlqRepo       = dlqRepo;
    _importJobRepo = importJobRepo;
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
    await _unitOfWork.SaveChangesAsync(cancellationToken);

    // MarkRetrying clears NextRetryAt so the polling worker doesn't double-pick it
    entry.MarkRetrying();
    await _dlqRepo.UpdateAsync(entry, cancellationToken);
    await _unitOfWork.SaveChangesAsync(cancellationToken);

    _queue.Enqueue(new IndexingWorkItem(job.Id, entry.StoragePath, entry.ContentType));

    return Result.Success();
  }
}
