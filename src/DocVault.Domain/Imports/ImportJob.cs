using DocVault.Domain.Primitives;

namespace DocVault.Domain.Imports;

public class ImportJob : AggregateRoot<Guid>
{
  public string FileName { get; private set; }
  public ImportStatus Status { get; private set; }
  public DateTime StartedAt { get; private set; } = DateTime.UtcNow;
  public DateTime? CompletedAt { get; private set; }
  public string? Error { get; private set; }

  private ImportJob() : base(Guid.Empty)
  {
    FileName = string.Empty;
  }

  public ImportJob(Guid id, string fileName) : base(id)
  {
    FileName = fileName;
    Status = ImportStatus.Pending;
  }

  public void MarkInProgress() => Status = ImportStatus.InProgress;
  public void MarkCompleted()
  {
    Status = ImportStatus.Completed;
    CompletedAt = DateTime.UtcNow;
  }

  public void MarkFailed(string error)
  {
    Status = ImportStatus.Failed;
    Error = error;
    CompletedAt = DateTime.UtcNow;
  }
}
