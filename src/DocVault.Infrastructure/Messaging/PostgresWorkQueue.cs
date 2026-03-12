using System.Data;
using DocVault.Application.Background.Queue;
using DocVault.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DocVault.Infrastructure.Messaging;

/// <summary>
/// Durable work queue backed by PostgreSQL using <c>SELECT … FOR UPDATE SKIP LOCKED</c>.
/// <para>
/// Survives process restarts — work items are persisted in the <c>IndexingQueue</c> table
/// and only removed once a worker claims them.  Multiple concurrent workers are safe:
/// SKIP LOCKED ensures each row is claimed by exactly one reader.
/// </para>
/// </summary>
public sealed partial class PostgresWorkQueue : IWorkQueue<IndexingWorkItem>
{
  private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(500);

  private readonly IDbContextFactory<DocVaultDbContext> _factory;
  private readonly ILogger<PostgresWorkQueue> _logger;

  /// <summary>
  /// Initialises the queue with a <see cref="IDbContextFactory{TContext}"/> (Singleton-safe)
  /// and a logger.
  /// </summary>
  /// <param name="factory">Factory used to create short-lived <see cref="DocVaultDbContext"/> instances.</param>
  /// <param name="logger">Logger for diagnostic messages.</param>
  public PostgresWorkQueue(
    IDbContextFactory<DocVaultDbContext> factory,
    ILogger<PostgresWorkQueue> logger)
  {
    _factory = factory;
    _logger  = logger;
  }

  // ---------------------------------------------------------------------------
  // IWorkQueue<IndexingWorkItem>
  // ---------------------------------------------------------------------------

  /// <inheritdoc />
  public void Enqueue(IndexingWorkItem workItem)
  {
    using var ctx = _factory.CreateDbContext();
    ctx.IndexingQueue.Add(new IndexingQueueEntry
    {
      JobId       = workItem.JobId,
      StoragePath = workItem.StoragePath,
      ContentType = workItem.ContentType,
      EnqueuedAt  = DateTime.UtcNow,
    });
    ctx.SaveChanges();
    LogEnqueued(workItem.JobId);
  }

  /// <inheritdoc />
  public bool TryDequeue(out IndexingWorkItem? workItem)
  {
    using var ctx = _factory.CreateDbContext();
    var conn = ctx.Database.GetDbConnection();

    if (conn.State != ConnectionState.Open)
      conn.Open();

    using var cmd = conn.CreateCommand();
    cmd.CommandText =
      """
      DELETE FROM "IndexingQueue"
      WHERE "Id" = (
          SELECT "Id" FROM "IndexingQueue"
          ORDER BY "EnqueuedAt"
          FOR UPDATE SKIP LOCKED
          LIMIT 1
      )
      RETURNING "Id", "JobId", "StoragePath", "ContentType", "EnqueuedAt"
      """;

    using var reader = cmd.ExecuteReader();

    if (!reader.Read())
    {
      workItem = null;
      return false;
    }

    workItem = new IndexingWorkItem(
      reader.GetGuid(reader.GetOrdinal("JobId")),
      reader.GetString(reader.GetOrdinal("StoragePath")),
      reader.GetString(reader.GetOrdinal("ContentType")));

    return true;
  }

  /// <inheritdoc />
  /// <remarks>
  /// Polls the database at <see cref="PollInterval"/> intervals until an item becomes
  /// available or <paramref name="cancellationToken"/> is cancelled.
  /// </remarks>
  public async ValueTask<IndexingWorkItem> DequeueAsync(CancellationToken cancellationToken = default)
  {
    while (!cancellationToken.IsCancellationRequested)
    {
      if (TryDequeue(out var item) && item is not null)
        return item;

      await Task.Delay(PollInterval, cancellationToken).ConfigureAwait(false);
    }

    throw new OperationCanceledException(cancellationToken);
  }

  // ---------------------------------------------------------------------------
  // Source-generated log messages
  // ---------------------------------------------------------------------------

  [LoggerMessage(Level = LogLevel.Debug, Message = "Enqueued indexing work item for job {JobId}.")]
  private partial void LogEnqueued(Guid jobId);
}
