namespace DocVault.Application.Background;

public sealed class BlobCleanupWorkerOptions
{
  public const string SectionName = "BlobCleanup";

  /// <summary>How often to retry queued pending deletions (seconds). Default: 5 minutes.</summary>
  public int RetryIntervalSeconds { get; set; } = 300;

  /// <summary>How often to run a full storage reconciliation scan (hours). Default: 24 hours.</summary>
  public int ReconciliationIntervalHours { get; set; } = 24;

  /// <summary>Maximum delivery attempts before an entry is abandoned. Default: 10.</summary>
  public int MaxRetryAttempts { get; set; } = 10;

  /// <summary>
  /// Blobs written within this many minutes of the reconciliation scan are skipped to avoid
  /// racing with in-flight uploads whose DB transaction has not yet committed. Default: 60 minutes.
  /// </summary>
  public int ReconciliationGraceMinutes { get; set; } = 60;
}
