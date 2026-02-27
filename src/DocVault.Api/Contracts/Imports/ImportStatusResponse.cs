namespace DocVault.Api.Contracts.Imports;

public sealed record ImportStatusResponse(Guid Id, string FileName, string Status, DateTime StartedAt, DateTime? CompletedAt, string? Error);
