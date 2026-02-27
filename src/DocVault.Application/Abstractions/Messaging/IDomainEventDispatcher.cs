namespace DocVault.Application.Abstractions.Messaging;

public interface IDomainEventDispatcher
{
  Task DispatchAsync(IEnumerable<object> domainEvents, CancellationToken cancellationToken = default);
}
