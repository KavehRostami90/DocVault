namespace DocVault.Application.Background;

public sealed class IndexingWorkerOptions
{
  public const string SectionName = "IndexingWorker";

  /// <summary>Max documents processed concurrently. Controls back-pressure across the embedding API, DB pool, and file storage.</summary>
  public int MaxDegreeOfParallelism { get; set; } = 4;

  /// <summary>Seconds to wait for in-flight jobs after shutdown before cancelling them. Cancelled jobs are recovered on next startup.</summary>
  public int DrainTimeoutSeconds { get; set; } = 30;
}
