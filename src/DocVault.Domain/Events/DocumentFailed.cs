using DocVault.Domain.Documents;

namespace DocVault.Domain.Events;

/// <summary>Domain event raised when document indexing fails.</summary>
public record DocumentFailed(DocumentId DocumentId, string? Error) : IDomainEvent
{
  public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
