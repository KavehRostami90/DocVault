using DocVault.Application.Abstractions.Persistence;
using DocVault.Application.Background.Queue;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DocVault.Application.Background;

/// <summary>
/// Polls the dead-letter queue and re-enqueues entries whose NextRetryAt has elapsed.
/// Runs independently from IndexingWorker so that transient failures never block
/// the main processing loop.
/// </summary>
public sealed partial class DeadLetterRetryWorker : BackgroundService
{
  private readonly IWorkQueue<IndexingWorkItem> _queue;
  private readonly IServiceScopeFactory        _scopeFactory;
  private readonly IndexingWorkerOptions        _options;
  private readonly ILogger<DeadLetterRetryWorker> _logger;

  public DeadLetterRetryWorker(
    IWorkQueue<IndexingWorkItem> queue,
    IServiceScopeFactory scopeFactory,
    IOptions<IndexingWorkerOptions> options,
    ILogger<DeadLetterRetryWorker> logger)
  {
    _queue        = queue;
    _scopeFactory = scopeFactory;
    _options      = options.Value;
    _logger       = logger;
  }

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    LogWorkerStarted(_logger, _options.DeadLetterPollingSeconds);

    while (!stoppingToken.IsCancellationRequested)
    {
      try
      {
        await Task.Delay(TimeSpan.FromSeconds(_options.DeadLetterPollingSeconds), stoppingToken);
      }
      catch (OperationCanceledException)
      {
        break;
      }

      try
      {
        await ProcessDueRetriesAsync(stoppingToken);
      }
      catch (Exception ex)
      {
        LogPollError(_logger, ex);
      }
    }
  }

  private async Task ProcessDueRetriesAsync(CancellationToken ct)
  {
    using var scope        = _scopeFactory.CreateScope();
    var dlqRepo            = scope.ServiceProvider.GetRequiredService<IFailedIndexingJobRepository>();
    var importJobRepo      = scope.ServiceProvider.GetRequiredService<IImportJobRepository>();
    var unitOfWork         = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

    var due = await dlqRepo.GetDueForRetryAsync(ct);
    if (due.Count == 0) return;

    LogFound(_logger, due.Count);

    foreach (var entry in due)
    {
      var job = await importJobRepo.GetAsync(entry.JobId, ct);
      if (job is null)
      {
        LogJobMissing(_logger, entry.JobId);
        entry.Discard();
        await dlqRepo.UpdateAsync(entry, ct);
        await unitOfWork.SaveChangesAsync(ct);
        continue;
      }

      // Reset ImportJob so crash recovery can find it if the process dies mid-retry.
      job.MarkPendingRetry();
      await importJobRepo.UpdateAsync(job, ct);

      // Clear NextRetryAt so this entry is not picked up again while in-flight.
      entry.MarkRetrying();
      await dlqRepo.UpdateAsync(entry, ct);

      await unitOfWork.SaveChangesAsync(ct);

      _queue.Enqueue(new IndexingWorkItem(job.Id, entry.StoragePath, entry.ContentType));
      LogRequeued(_logger, entry.JobId, entry.AttemptCount + 1, _options.MaxRetryAttempts);
    }
  }

  [LoggerMessage(0, LogLevel.Information,
    "DeadLetterRetryWorker started — polling every {PollSeconds}s.")]
  private static partial void LogWorkerStarted(ILogger logger, int pollSeconds);

  [LoggerMessage(1, LogLevel.Information,
    "DeadLetterRetryWorker found {Count} entry(ies) due for retry.")]
  private static partial void LogFound(ILogger logger, int count);

  [LoggerMessage(2, LogLevel.Information,
    "Re-enqueued dead-letter job {JobId} for retry attempt {Attempt}/{Max}.")]
  private static partial void LogRequeued(ILogger logger, Guid jobId, int attempt, int max);

  [LoggerMessage(3, LogLevel.Warning,
    "Dead-letter entry references missing ImportJob {JobId} — discarding.")]
  private static partial void LogJobMissing(ILogger logger, Guid jobId);

  [LoggerMessage(4, LogLevel.Error,
    "Dead-letter retry worker encountered an error during polling.")]
  private static partial void LogPollError(ILogger logger, Exception ex);
}
