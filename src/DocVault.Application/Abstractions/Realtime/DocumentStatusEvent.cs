namespace DocVault.Application.Abstractions.Realtime;

public sealed record DocumentStatusEvent(Guid DocumentId, string Status, string? Error = null);
