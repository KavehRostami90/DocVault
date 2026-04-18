using DocVault.Domain.Documents;

namespace DocVault.Application.Abstractions.Realtime;

public sealed record DocumentStatusEvent(Guid DocumentId, DocumentStatus Status, string? Error = null);
