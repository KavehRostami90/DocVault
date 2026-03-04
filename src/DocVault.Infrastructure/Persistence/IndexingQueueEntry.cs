namespace DocVault.Infrastructure.Persistence;

/// <summary>
/// Persisted row in the <c>IndexingQueue</c> table.
/// Rows are dequeued with <c>SELECT … FOR UPDATE SKIP LOCKED</c>
/// so concurrent workers never process the same item.
/// </summary>
public sealed class IndexingQueueEntry
{
  /// <summary>Auto-incremented surrogate key; used to maintain FIFO order.</summary>
  public long Id { get; init; }

  public Guid JobId { get; init; }

  public string StoragePath { get; init; } = string.Empty;

  public string ContentType { get; init; } = string.Empty;

  public DateTime EnqueuedAt { get; init; }
}
