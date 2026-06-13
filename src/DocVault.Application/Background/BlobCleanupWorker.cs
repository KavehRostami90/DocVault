using DocVault.Application.Abstractions.Persistence;
using DocVault.Application.Abstractions.Storage;
using DocVault.Domain.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DocVault.Application.Background;

/// <summary>
/// Background service with two responsibilities:
/// 1. Retry loop — periodically retries blobs recorded in <c>PendingBlobDeletions</c>.
/// 2. Reconciliation scan — lists all blobs in storage, compares against document IDs in the
///    database, and enqueues any orphans that have no matching document record.
/// </summary>
public sealed partial class BlobCleanupWorker : BackgroundService
{
  private readonly IServiceScopeFactory _scopeFactory;
  private readonly IFileStorage _storage;
  private readonly BlobCleanupWorkerOptions _options;
  private readonly ILogger<BlobCleanupWorker> _logger;

  public BlobCleanupWorker(
    IServiceScopeFactory scopeFactory,
    IFileStorage storage,
    IOptions<BlobCleanupWorkerOptions> options,
    ILogger<BlobCleanupWorker> logger)
  {
    _scopeFactory = scopeFactory;
    _storage      = storage;
    _options      = options.Value;
    _logger       = logger;
  }

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    LogWorkerStarted(_logger, _options.RetryIntervalSeconds, _options.ReconciliationIntervalHours);

    // Defer the first reconciliation by one full interval so it doesn't race with
    // document writes during application startup.
    var nextReconciliation = DateTimeOffset.UtcNow.AddHours(_options.ReconciliationIntervalHours);

    while (!stoppingToken.IsCancellationRequested)
    {
      try
      {
        await RetryPendingDeletionsAsync(stoppingToken);

        if (DateTimeOffset.UtcNow >= nextReconciliation)
        {
          await ReconcileStorageAsync(stoppingToken);
          nextReconciliation = DateTimeOffset.UtcNow.AddHours(_options.ReconciliationIntervalHours);
        }
      }
      catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
      {
        break;
      }
      catch (Exception ex)
      {
        LogWorkerError(_logger, ex);
      }

      try
      {
        await Task.Delay(TimeSpan.FromSeconds(_options.RetryIntervalSeconds), stoppingToken);
      }
      catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
      {
        break;
      }
    }
  }

  private async Task RetryPendingDeletionsAsync(CancellationToken ct)
  {
    using var scope = _scopeFactory.CreateScope();
    var repo       = scope.ServiceProvider.GetRequiredService<IPendingBlobDeletionRepository>();
    var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

    var pending = await repo.GetPendingAsync(_options.MaxRetryAttempts, ct);
    if (pending.Count == 0) return;

    LogRetryingPending(_logger, pending.Count);

    foreach (var entry in pending)
    {
      if (ct.IsCancellationRequested) break;
      await TryDeleteAndUpdateAsync(entry, repo, unitOfWork, ct);
    }
  }

  private async Task ReconcileStorageAsync(CancellationToken ct)
  {
    LogReconciliationStarted(_logger);

    var blobs = await _storage.ListAsync(ct);
    if (blobs.Count == 0)
    {
      LogReconciliationComplete(_logger, 0);
      return;
    }

    using var scope     = _scopeFactory.CreateScope();
    var docRepo         = scope.ServiceProvider.GetRequiredService<IDocumentRepository>();
    var pendingRepo     = scope.ServiceProvider.GetRequiredService<IPendingBlobDeletionRepository>();
    var unitOfWork      = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

    var knownIds = await docRepo.GetAllIdsAsync(ct);
    var knownPaths = knownIds.Select(id => $"{id}.bin").ToHashSet(StringComparer.OrdinalIgnoreCase);

    var orphaned = blobs.Where(b => !knownPaths.Contains(b)).ToList();
    if (orphaned.Count == 0)
    {
      LogReconciliationComplete(_logger, 0);
      return;
    }

    LogOrphansFound(_logger, orphaned.Count);

    int deleted = 0;
    int enqueued = 0;

    foreach (var path in orphaned)
    {
      if (ct.IsCancellationRequested) break;

      try
      {
        await _storage.DeleteAsync(path, ct);
        deleted++;
        LogOrphanDeleted(_logger, path);
      }
      catch (Exception ex)
      {
        // Couldn't delete right now — queue it for retry if not already queued
        try
        {
          if (!await pendingRepo.ExistsByPathAsync(path, ct))
          {
            var entry = new PendingBlobDeletion(Guid.NewGuid(), path);
            await pendingRepo.AddAsync(entry, ct);
            await unitOfWork.SaveChangesAsync(ct);
            enqueued++;
          }
        }
        catch (Exception queueEx)
        {
          LogEnqueueFailed(_logger, path, queueEx);
        }

        LogOrphanDeleteFailed(_logger, path, ex);
      }
    }

    LogReconciliationComplete(_logger, deleted + enqueued);
  }

  private async Task TryDeleteAndUpdateAsync(
    PendingBlobDeletion entry,
    IPendingBlobDeletionRepository repo,
    IUnitOfWork unitOfWork,
    CancellationToken ct)
  {
    try
    {
      await _storage.DeleteAsync(entry.StoragePath, ct);
      await repo.DeleteAsync(entry.Id, ct);
      await unitOfWork.SaveChangesAsync(ct);
      LogPendingDeleted(_logger, entry.StoragePath, entry.AttemptCount + 1);
    }
    catch (Exception ex)
    {
      entry.RecordAttempt(ex.Message);
      await repo.UpdateAsync(entry, ct);
      await unitOfWork.SaveChangesAsync(ct);

      if (entry.AttemptCount >= _options.MaxRetryAttempts)
        LogPendingAbandoned(_logger, entry.StoragePath, entry.AttemptCount);
      else
        LogPendingRetryFailed(_logger, entry.StoragePath, entry.AttemptCount, ex);
    }
  }

  [LoggerMessage(0, LogLevel.Information,
    "BlobCleanupWorker started — retry every {RetryIntervalSeconds}s, reconciliation every {ReconciliationIntervalHours}h.")]
  private static partial void LogWorkerStarted(ILogger l, int retryIntervalSeconds, int reconciliationIntervalHours);

  [LoggerMessage(1, LogLevel.Warning,
    "BlobCleanupWorker encountered an unhandled error.")]
  private static partial void LogWorkerError(ILogger l, Exception ex);

  [LoggerMessage(2, LogLevel.Information,
    "Retrying {Count} pending blob deletion(s).")]
  private static partial void LogRetryingPending(ILogger l, int count);

  [LoggerMessage(3, LogLevel.Information,
    "Blob '{StoragePath}' deleted successfully after {AttemptCount} attempt(s).")]
  private static partial void LogPendingDeleted(ILogger l, string storagePath, int attemptCount);

  [LoggerMessage(4, LogLevel.Warning,
    "Failed to delete blob '{StoragePath}' on attempt {AttemptCount}.")]
  private static partial void LogPendingRetryFailed(ILogger l, string storagePath, int attemptCount, Exception ex);

  [LoggerMessage(5, LogLevel.Error,
    "Blob '{StoragePath}' has reached the maximum retry limit ({AttemptCount} attempts) and will no longer be retried.")]
  private static partial void LogPendingAbandoned(ILogger l, string storagePath, int attemptCount);

  [LoggerMessage(6, LogLevel.Information,
    "Storage reconciliation scan started.")]
  private static partial void LogReconciliationStarted(ILogger l);

  [LoggerMessage(7, LogLevel.Information,
    "Storage reconciliation complete — {Count} orphan(s) resolved.")]
  private static partial void LogReconciliationComplete(ILogger l, int count);

  [LoggerMessage(8, LogLevel.Warning,
    "Found {Count} orphaned blob(s) with no matching document record.")]
  private static partial void LogOrphansFound(ILogger l, int count);

  [LoggerMessage(9, LogLevel.Information,
    "Orphaned blob '{StoragePath}' deleted.")]
  private static partial void LogOrphanDeleted(ILogger l, string storagePath);

  [LoggerMessage(10, LogLevel.Warning,
    "Failed to delete orphaned blob '{StoragePath}' — queuing for retry.")]
  private static partial void LogOrphanDeleteFailed(ILogger l, string storagePath, Exception ex);

  [LoggerMessage(11, LogLevel.Error,
    "Failed to enqueue orphaned blob '{StoragePath}' for retry.")]
  private static partial void LogEnqueueFailed(ILogger l, string storagePath, Exception ex);
}
