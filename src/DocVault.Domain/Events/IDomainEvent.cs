namespace DocVault.Domain.Events;

public interface IDomainEvent
{
  DateTime OccurredOn { get; }
}
