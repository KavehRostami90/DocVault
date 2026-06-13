namespace DocVault.Domain.Storage;

/// <summary>
/// Tracks a storage blob that could not be deleted immediately and needs a deferred retry.
/// Created when <c>DeleteDocumentHandler</c> fails to remove the file from storage after
/// successfully deleting the database record.
/// </summary>
public class PendingBlobDeletion
{
  public Guid Id { get; private set; }

  /// <summary>Relative path within the storage backend (e.g. "abc123.bin").</summary>
  public string StoragePath { get; private set; }

  public DateTimeOffset CreatedAt { get; private set; }
  public int AttemptCount { get; private set; }
  public DateTimeOffset? LastAttemptAt { get; private set; }
  public string? LastError { get; private set; }

  // EF Core constructor
  private PendingBlobDeletion()
  {
    StoragePath = string.Empty;
  }

  public PendingBlobDeletion(Guid id, string storagePath)
  {
    Id          = id;
    StoragePath = storagePath;
    CreatedAt   = DateTimeOffset.UtcNow;
  }

  public void RecordAttempt(string? error)
  {
    AttemptCount++;
    LastAttemptAt = DateTimeOffset.UtcNow;
    LastError     = error;
  }
}
