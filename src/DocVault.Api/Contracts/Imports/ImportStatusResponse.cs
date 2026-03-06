namespace DocVault.Api.Contracts.Imports;

/// <summary>
/// Status details for a document import.
/// </summary>
/// <param name="Id">Unique identifier of the import job.</param>
/// <param name="FileName">Original file name being imported.</param>
/// <param name="Status">Current status of the import job.</param>
/// <param name="StartedAt">Timestamp when the import started (UTC).</param>
/// <param name="CompletedAt">Timestamp when the import finished (UTC), if completed.</param>
/// <param name="Error">Error message if the import failed.</param>
public sealed record ImportStatusResponse(Guid Id, string FileName, string Status, DateTime StartedAt, DateTime? CompletedAt, string? Error);
