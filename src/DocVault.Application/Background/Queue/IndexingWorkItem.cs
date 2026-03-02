namespace DocVault.Application.Background.Queue;

/// <summary>
/// Payload placed on the indexing queue when a document is uploaded.
/// Carries everything the background worker needs without hitting the database
/// on the hot path.
/// </summary>
public sealed record IndexingWorkItem(Guid JobId, string StoragePath, string ContentType);
