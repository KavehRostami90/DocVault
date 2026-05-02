using System.Collections.Concurrent;
using DocVault.Application.Abstractions.Messaging;
using DocVault.Application.Abstractions.Persistence;
using DocVault.Application.Background.Queue;
using DocVault.Application.Pipeline;
using DocVault.Domain.Documents;
using DocVault.Domain.Imports;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DocVault.Application.Background;

/// <summary>Background service that drains the indexing queue. Recovers InProgress jobs on startup.</summary>
public sealed partial class IndexingWorker : BackgroundService
{
  private readonly IWorkQueue<IndexingWorkItem> _queue;
  private readonly IIngestionPipeline _pipeline;
  private readonly IServiceScopeFactory _scopeFactory;
  private readonly IDomainEventDispatcher _eventDispatcher;
  private readonly IndexingWorkerOptions _options;
  private readonly ILogger<IndexingWorker> _logger;

  public IndexingWorker(
    IWorkQueue<IndexingWorkItem> queue,
    IIngestionPipeline pipeline,
    IServiceScopeFactory scopeFactory,
    IDomainEventDispatcher eventDispatcher,
    IOptions<IndexingWorkerOptions> options,
    ILogger<IndexingWorker> logger)
  {
    _queue           = queue;
    _pipeline        = pipeline;
    _scopeFactory    = scopeFactory;
    _eventDispatcher = eventDispatcher;
    _options         = options.Value;
    _logger          = logger;
  }

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    try
    {
      await RecoverPendingJobsAsync(stoppingToken);
    }
    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
    {
      return;
    }

    LogWorkerStarted(_logger, _options.MaxDegreeOfParallelism);

    using var jobCts     = new CancellationTokenSource();
    using var semaphore  = new SemaphoreSlim(_options.MaxDegreeOfParallelism, _options.MaxDegreeOfParallelism);
    var       activeTasks = new ConcurrentDictionary<Task, byte>();

    while (!stoppingToken.IsCancellationRequested)
    {
      try
      {
        await semaphore.WaitAsync(stoppingToken);
      }
      catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
      {
        break;
      }

      IndexingWorkItem item;
      try
      {
        item = await _queue.DequeueAsync(stoppingToken);
      }
      catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
      {
        semaphore.Release();
        break;
      }

      var task = ProcessAndReleaseAsync(item, semaphore, jobCts.Token);
      activeTasks.TryAdd(task, 0);
      _ = task.ContinueWith(t => activeTasks.TryRemove(t, out _), TaskScheduler.Default);
    }

    if (activeTasks.Count == 0)
      return;

    LogDraining(_logger, activeTasks.Count);

    var drainTimeout = TimeSpan.FromSeconds(_options.DrainTimeoutSeconds);
    var drainTask    = Task.WhenAll(activeTasks.Keys);
    var timedOut     = await Task.WhenAny(drainTask, Task.Delay(drainTimeout)) != drainTask;

    if (timedOut)
    {
      var remaining = activeTasks.Keys.Count(t => !t.IsCompleted);
      LogDrainTimedOut(_logger, remaining, _options.DrainTimeoutSeconds);
      await jobCts.CancelAsync();
      try { await drainTask.WaitAsync(TimeSpan.FromSeconds(5)); }
      catch { }
    }
    else
    {
      try { await drainTask; }
      catch { }
    }
  }

  private async Task ProcessAndReleaseAsync(
    IndexingWorkItem item,
    SemaphoreSlim semaphore,
    CancellationToken ct)
  {
    try
    {
      await ProcessItemAsync(item, ct);
    }
    catch (Exception ex) when (!ct.IsCancellationRequested)
    {
      LogUnhandledWorkerError(_logger, item.JobId, ex);
    }
    finally
    {
      semaphore.Release();
    }
  }

  private async Task RecoverPendingJobsAsync(CancellationToken ct)
  {
    using var scope = _scopeFactory.CreateScope();
    var repo = scope.ServiceProvider.GetRequiredService<IImportJobRepository>();

    var inProgress = await repo.GetInProgressAsync(ct);
    foreach (var job in inProgress)
      _queue.Enqueue(new IndexingWorkItem(job.Id, job.StoragePath, job.ContentType));

    if (inProgress.Count > 0)
      LogRecovered(_logger, inProgress.Count);
  }

  private async Task ProcessItemAsync(IndexingWorkItem item, CancellationToken ct)
  {
    using var scope = _scopeFactory.CreateScope();
    var importJobRepository = scope.ServiceProvider.GetRequiredService<IImportJobRepository>();
    var documentRepository  = scope.ServiceProvider.GetRequiredService<IDocumentRepository>();
    var unitOfWork          = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

    var job = await importJobRepository.GetAsync(item.JobId, ct);
    if (job is null)
    {
      LogJobNotFound(_logger, item.JobId);
      return;
    }

    job.MarkInProgress();
    await importJobRepository.UpdateAsync(job, ct);
    await unitOfWork.SaveChangesAsync(ct);

    try
    {
      LogProcessing(_logger, item.JobId, item.StoragePath);
      var result = await RunPipelineAsync(item, ct);

      job.MarkCompleted();
      await importJobRepository.UpdateAsync(job, ct);

      var document = await documentRepository.GetAsync(job.DocumentId, ct);
      if (document is not null)
      {
        document.AttachText(result.Text);

        if (result.Chunks.Count > 0)
        {
          document.AttachEmbedding(result.Chunks[0].Embedding);

          var chunkRepository = scope.ServiceProvider.GetRequiredService<IDocumentChunkRepository>();
          var domainChunks = result.Chunks
            .Select(ce =>
            {
              var c = DocumentChunk.Create(
                document.Id,
                ce.Chunk.Index,
                ce.Chunk.Text,
                ce.Chunk.StartChar,
                ce.Chunk.EndChar);
              c.AttachEmbedding(ce.Embedding);
              return c;
            })
            .ToList();

          await chunkRepository.ReplaceAsync(document.Id, domainChunks, ct);
        }

        document.MarkIndexed();
        await documentRepository.UpdateAsync(document, ct);
      }

      await unitOfWork.SaveChangesAsync(ct);

      if (document is not null)
      {
        await _eventDispatcher.DispatchAsync(document.DomainEvents, ct);
        document.ClearDomainEvents();
      }

      LogCompleted(_logger, item.JobId);
    }
    catch (Exception ex) when (!ct.IsCancellationRequested)
    {
      LogFailed(_logger, item.JobId, ex);
      await MarkFailedAsync(job, item.JobId, ex.Message, importJobRepository, documentRepository, unitOfWork, ct);
    }
  }

  private Task<IngestionResult> RunPipelineAsync(IndexingWorkItem item, CancellationToken ct)
    => _pipeline.RunAsync(item.StoragePath, item.ContentType, ct);

  private async Task MarkFailedAsync(
    ImportJob job,
    Guid jobId,
    string errorMessage,
    IImportJobRepository importJobRepository,
    IDocumentRepository documentRepository,
    IUnitOfWork unitOfWork,
    CancellationToken ct)
  {
    try
    {
      job.MarkFailed(errorMessage);
      await importJobRepository.UpdateAsync(job, ct);

      var document = await documentRepository.GetAsync(job.DocumentId, ct);
      if (document is not null)
      {
        document.MarkFailed(errorMessage);
        await documentRepository.UpdateAsync(document, ct);
      }

      await unitOfWork.SaveChangesAsync(ct);

      if (document is not null)
      {
        await _eventDispatcher.DispatchAsync(document.DomainEvents, ct);
        document.ClearDomainEvents();
      }
    }
    catch (Exception updateEx)
    {
      LogStatusUpdateFailed(_logger, jobId, updateEx);
    }
  }

  [LoggerMessage(0, LogLevel.Information,
    "IndexingWorker started — MaxDegreeOfParallelism={MaxDegreeOfParallelism}.")]
  private static partial void LogWorkerStarted(ILogger logger, int maxDegreeOfParallelism);

  [LoggerMessage(1, LogLevel.Information,
    "Recovered {Count} pending indexing job(s) from the database and re-enqueued them.")]
  private static partial void LogRecovered(ILogger logger, int count);

  [LoggerMessage(2, LogLevel.Information,
    "Processing indexing job {JobId} — storage path: {StoragePath}")]
  private static partial void LogProcessing(ILogger logger, Guid jobId, string storagePath);

  [LoggerMessage(3, LogLevel.Information, "Indexing job {JobId} completed successfully.")]
  private static partial void LogCompleted(ILogger logger, Guid jobId);

  [LoggerMessage(4, LogLevel.Error, "Indexing job {JobId} failed.")]
  private static partial void LogFailed(ILogger logger, Guid jobId, Exception ex);

  [LoggerMessage(5, LogLevel.Warning, "Indexing job {JobId} not found in the database; skipping.")]
  private static partial void LogJobNotFound(ILogger logger, Guid jobId);

  [LoggerMessage(6, LogLevel.Error,
    "Failed to update status for indexing job {JobId} after a processing error.")]
  private static partial void LogStatusUpdateFailed(ILogger logger, Guid jobId, Exception ex);

  [LoggerMessage(7, LogLevel.Information,
    "IndexingWorker stopping — draining {Count} in-flight job(s).")]
  private static partial void LogDraining(ILogger logger, int count);

  [LoggerMessage(8, LogLevel.Error,
    "Unhandled error in IndexingWorker for job {JobId} — error escaped the inner handler.")]
  private static partial void LogUnhandledWorkerError(ILogger logger, Guid jobId, Exception ex);

  [LoggerMessage(9, LogLevel.Warning,
    "IndexingWorker drain timed out after {DrainTimeoutSeconds}s — {Count} job(s) did not finish and will be recovered on next startup.")]
  private static partial void LogDrainTimedOut(ILogger logger, int count, int drainTimeoutSeconds);
}
