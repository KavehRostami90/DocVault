using DocVault.Domain.Primitives;

namespace DocVault.Domain.Imports;

public class FailedIndexingJob : AggregateRoot<Guid>
{
    public Guid   JobId        { get; private set; }
    public string StoragePath  { get; private set; }
    public string ContentType  { get; private set; }
    public int    AttemptCount { get; private set; }
    public int    MaxAttempts  { get; private set; }
    public string LastError    { get; private set; }

    public DateTimeOffset  FirstFailedAt { get; private set; }
    public DateTimeOffset  LastFailedAt  { get; private set; }

    /// <summary>
    /// When to next retry. Null while a retry is in-flight (claimed by the retry worker)
    /// or when the entry is exhausted.
    /// </summary>
    public DateTimeOffset? NextRetryAt { get; private set; }
    public bool            IsExhausted { get; private set; }

    // EF Core
    private FailedIndexingJob() : base(Guid.Empty)
    {
        StoragePath = string.Empty;
        ContentType = string.Empty;
        LastError   = string.Empty;
    }

    /// <summary>Creates the first DLQ entry for a job that just failed.</summary>
    public FailedIndexingJob(
        Guid jobId,
        string storagePath,
        string contentType,
        int maxAttempts,
        string lastError,
        DateTimeOffset? nextRetryAt)
        : base(Guid.NewGuid())
    {
        JobId         = jobId;
        StoragePath   = storagePath;
        ContentType   = contentType;
        MaxAttempts   = maxAttempts;
        LastError     = lastError;
        FirstFailedAt = DateTimeOffset.UtcNow;
        LastFailedAt  = DateTimeOffset.UtcNow;
        AttemptCount  = 1;
        NextRetryAt   = nextRetryAt;
        IsExhausted   = nextRetryAt is null;
    }

    /// <summary>Records another failure on an existing entry and schedules the next retry.</summary>
    public void RecordFailure(string error, DateTimeOffset? nextRetryAt)
    {
        AttemptCount++;
        LastError    = error;
        LastFailedAt = DateTimeOffset.UtcNow;
        NextRetryAt  = nextRetryAt;
        IsExhausted  = nextRetryAt is null;
    }

    /// <summary>
    /// Called by the retry worker when it claims this entry for re-processing.
    /// Clears NextRetryAt so the entry is not claimed again while in flight.
    /// </summary>
    public void MarkRetrying() => NextRetryAt = null;

    /// <summary>Admin: force immediate retry even if NextRetryAt is in the future.</summary>
    public void ScheduleImmediateRetry()
    {
        IsExhausted = false;
        NextRetryAt = DateTimeOffset.UtcNow.AddSeconds(-1);
    }

    /// <summary>Admin: permanently discard — marks the job as permanently failed.</summary>
    public void Discard()
    {
        IsExhausted = true;
        NextRetryAt = null;
    }
}
