using DocVault.Domain.Documents;

namespace DocVault.Domain.Events;

public record DocumentIndexed(DocumentId DocumentId) : IDomainEvent
{
  public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
