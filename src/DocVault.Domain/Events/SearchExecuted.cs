namespace DocVault.Domain.Events;

public record SearchExecuted(string Query) : IDomainEvent
{
  public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
