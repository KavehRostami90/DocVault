namespace DocVault.Application.Background;

public sealed class IndexingWorkerOptions
{
  public const string SectionName = "IndexingWorker";

  /// <summary>Max documents processed concurrently. Controls back-pressure across the embedding API, DB pool, and file storage.</summary>
  public int MaxDegreeOfParallelism { get; set; } = 4;

  /// <summary>Seconds to wait for in-flight jobs after shutdown before cancelling them. Cancelled jobs are recovered on next startup.</summary>
  public int DrainTimeoutSeconds { get; set; } = 30;

  /// <summary>How many times to attempt indexing a document before moving it to the exhausted state.</summary>
  public int MaxRetryAttempts { get; set; } = 3;

  /// <summary>
  /// Base delay in minutes for the exponential back-off between retry attempts.
  /// Actual delays: attempt 1 → base, attempt 2 → base×5, attempt 3 → base×25.
  /// </summary>
  public int RetryBaseDelayMinutes { get; set; } = 5;

  /// <summary>How often (in seconds) the dead-letter retry worker polls for due entries.</summary>
  public int DeadLetterPollingSeconds { get; set; } = 60;
}
