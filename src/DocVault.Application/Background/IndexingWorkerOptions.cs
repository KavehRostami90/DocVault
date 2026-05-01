namespace DocVault.Application.Background;

public sealed class IndexingWorkerOptions
{
  public const string SectionName = "IndexingWorker";

  /// <summary>
  /// Maximum number of documents processed concurrently.
  /// Acts as a single backpressure knob across all shared resources:
  /// the embedding API (RPM limits), the DB connection pool, CPU (OCR / PDF),
  /// and file storage I/O.
  ///
  /// Tune based on your actual bottleneck:
  ///   1-2  → strict embedding-API RPM limit (e.g. OpenAI free tier)
  ///   4    → balanced default for most deployments
  ///   8+   → generous API quota, slow storage, many small documents
  /// </summary>
  public int MaxDegreeOfParallelism { get; set; } = 4;

  /// <summary>
  /// Seconds to wait for in-flight indexing jobs to finish after a shutdown signal before
  /// forcefully cancelling them. Jobs cancelled this way remain <c>InProgress</c> in the
  /// database and are recovered on next startup.
  ///
  /// Must be &gt; 0. <c>HostOptions.ShutdownTimeout</c> is automatically configured to
  /// <c>DrainTimeoutSeconds + 15</c> so the host always outlasts the drain window.
  /// </summary>
  public int DrainTimeoutSeconds { get; set; } = 30;
}
