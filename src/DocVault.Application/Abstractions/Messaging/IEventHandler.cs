namespace DocVault.Application.Abstractions.Messaging;

public interface IEventHandler<in TEvent>
{
  Task HandleAsync(TEvent @event, CancellationToken cancellationToken = default);
}
