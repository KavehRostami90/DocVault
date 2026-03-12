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

  /// <summary>The identifier of the <see cref="DocVault.Domain.Imports.ImportJob"/> to be processed.</summary>
  public Guid JobId { get; init; }

  /// <summary>Relative path within the blob storage root where the file is persisted.</summary>
  public string StoragePath { get; init; } = string.Empty;

  /// <summary>MIME content type of the uploaded file (e.g. <c>application/pdf</c>).</summary>
  public string ContentType { get; init; } = string.Empty;

  /// <summary>UTC timestamp at which the entry was inserted; used for FIFO ordering.</summary>
  public DateTime EnqueuedAt { get; init; }
}
