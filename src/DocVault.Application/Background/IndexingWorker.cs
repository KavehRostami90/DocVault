using DocVault.Application.Abstractions.Persistence;
using DocVault.Application.Background.Queue;
using DocVault.Application.Pipeline;
using DocVault.Domain.Imports;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DocVault.Application.Background;

/// <summary>
/// Long-running hosted service that drains the indexing queue.
/// <para>
/// On startup it recovers any <c>Pending</c> or <c>InProgress</c> jobs
/// left behind by a previous crash.  During normal operation it awaits
/// each work item from the <see cref="IWorkQueue{T}"/> channel, executes
/// the <see cref="IngestionPipeline"/>, and updates the
/// <see cref="ImportJob"/> status in the database.
/// </para>
/// </summary>
public sealed partial class IndexingWorker : BackgroundService
{
  private readonly IWorkQueue<IndexingWorkItem> _queue;
  private readonly IIngestionPipeline _pipeline;
  private readonly IServiceScopeFactory _scopeFactory;
  private readonly ILogger<IndexingWorker> _logger;

  public IndexingWorker(
    IWorkQueue<IndexingWorkItem> queue,
    IIngestionPipeline pipeline,
    IServiceScopeFactory scopeFactory,
    ILogger<IndexingWorker> logger)
  {
    _queue        = queue;
    _pipeline     = pipeline;
    _scopeFactory = scopeFactory;
    _logger       = logger;
  }

  // -------------------------------------------------------------------------
  // BackgroundService
  // -------------------------------------------------------------------------

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    try
    {
      // Recover jobs that were queued but never processed (e.g. process crash).
      await RecoverPendingJobsAsync(stoppingToken);
    }
    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
    {
      return;
    }

    while (!stoppingToken.IsCancellationRequested)
    {
      IndexingWorkItem item;
      try
      {
        item = await _queue.DequeueAsync(stoppingToken);
      }
      catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
      {
        break;
      }

      await ProcessItemAsync(item, stoppingToken);
    }
  }

  // -------------------------------------------------------------------------
  // Recovery
  // -------------------------------------------------------------------------

  private async Task RecoverPendingJobsAsync(CancellationToken ct)
  {
    using var scope = _scopeFactory.CreateScope();
    var repo = scope.ServiceProvider.GetRequiredService<IImportJobRepository>();

    var pending = await repo.GetPendingAsync(ct);
    foreach (var job in pending)
      _queue.Enqueue(new IndexingWorkItem(job.Id, job.StoragePath, job.ContentType));

    if (pending.Count > 0)
      LogRecovered(_logger, pending.Count);
  }

  // -------------------------------------------------------------------------
  // Processing
  // -------------------------------------------------------------------------

  private async Task ProcessItemAsync(IndexingWorkItem item, CancellationToken ct)
  {
    using var scope = _scopeFactory.CreateScope();
    var importJobRepository = scope.ServiceProvider.GetRequiredService<IImportJobRepository>();
    var documentRepository  = scope.ServiceProvider.GetRequiredService<IDocumentRepository>();

    var job = await importJobRepository.GetAsync(item.JobId, ct);
    if (job is null)
    {
      LogJobNotFound(_logger, item.JobId);
      return;
    }

    job.MarkInProgress();
    await importJobRepository.UpdateAsync(job, ct);

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
        document.AttachEmbedding(result.Embedding);
        document.MarkIndexed();
        await documentRepository.UpdateAsync(document, ct);
      }

      LogCompleted(_logger, item.JobId);
    }
    catch (Exception ex) when (!ct.IsCancellationRequested)
    {
      LogFailed(_logger, item.JobId, ex);
      await MarkFailedAsync(job, item.JobId, ex.Message, importJobRepository, documentRepository, ct);
    }
  }

  /// <summary>Runs the ingestion pipeline stages for the given work item.</summary>
  private Task<IngestionResult> RunPipelineAsync(IndexingWorkItem item, CancellationToken ct)
    => _pipeline.RunAsync(item.StoragePath, item.ContentType, ct);

  /// <summary>
  /// Updates the job and document to <c>Failed</c> status after a processing error.
  /// Isolated so that state-transition errors don't mask the original exception.
  /// </summary>
  private async Task MarkFailedAsync(
    ImportJob job,
    Guid jobId,
    string errorMessage,
    IImportJobRepository importJobRepository,
    IDocumentRepository documentRepository,
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
    }
    catch (Exception updateEx)
    {
      LogStatusUpdateFailed(_logger, jobId, updateEx);
    }
  }

  // -------------------------------------------------------------------------
  // Source-generated log methods
  // -------------------------------------------------------------------------

  [LoggerMessage(1, LogLevel.Information,
    "Recovered {Count} pending indexing job(s) from the database and re-enqueued them.")]
  private static partial void LogRecovered(ILogger logger, int count);

  [LoggerMessage(2, LogLevel.Information,
    "Processing indexing job {JobId} — storage path: {StoragePath}")]
  private static partial void LogProcessing(ILogger logger, Guid jobId, string storagePath);

  [LoggerMessage(3, LogLevel.Information,
    "Indexing job {JobId} completed successfully.")]
  private static partial void LogCompleted(ILogger logger, Guid jobId);

  [LoggerMessage(4, LogLevel.Error,
    "Indexing job {JobId} failed.")]
  private static partial void LogFailed(ILogger logger, Guid jobId, Exception ex);

  [LoggerMessage(5, LogLevel.Warning,
    "Indexing job {JobId} not found in the database; skipping.")]
  private static partial void LogJobNotFound(ILogger logger, Guid jobId);

  [LoggerMessage(6, LogLevel.Error,
    "Failed to update status for indexing job {JobId} after a processing error.")]
  private static partial void LogStatusUpdateFailed(ILogger logger, Guid jobId, Exception ex);
}

