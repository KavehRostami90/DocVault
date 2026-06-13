namespace DocVault.Application.UseCases.Admin.DeadLetterQueue;

public sealed record FailedIndexingJobDto(
    Guid            Id,
    Guid            JobId,
    string          StoragePath,
    string          ContentType,
    int             AttemptCount,
    int             MaxAttempts,
    string          LastError,
    DateTimeOffset  FirstFailedAt,
    DateTimeOffset  LastFailedAt,
    DateTimeOffset? NextRetryAt,
    bool            IsExhausted);
