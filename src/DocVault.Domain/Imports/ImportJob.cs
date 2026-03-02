using DocVault.Domain.Primitives;

namespace DocVault.Domain.Imports;

public class ImportJob : AggregateRoot<Guid>
{
  public string FileName { get; private set; }
  /// <summary>Path within the file-storage service where the raw file is kept.</summary>
  public string StoragePath { get; private set; }
  /// <summary>MIME type of the file, e.g. application/pdf.</summary>
  public string ContentType { get; private set; }
  public ImportStatus Status { get; private set; }
  public DateTime StartedAt { get; private set; } = DateTime.UtcNow;
  public DateTime? CompletedAt { get; private set; }
  public string? Error { get; private set; }

  // EF Core constructor
  private ImportJob() : base(Guid.Empty)
  {
    FileName    = string.Empty;
    StoragePath = string.Empty;
    ContentType = string.Empty;
  }

  public ImportJob(Guid id, string fileName, string storagePath, string contentType) : base(id)
  {
    FileName    = fileName;
    StoragePath = storagePath;
    ContentType = contentType;
    Status      = ImportStatus.Pending;
  }

  public void MarkInProgress() => Status = ImportStatus.InProgress;
  public void MarkCompleted()
  {
    Status      = ImportStatus.Completed;
    CompletedAt = DateTime.UtcNow;
  }

  public void MarkFailed(string error)
  {
    Status      = ImportStatus.Failed;
    Error       = error;
    CompletedAt = DateTime.UtcNow;
  }
}

