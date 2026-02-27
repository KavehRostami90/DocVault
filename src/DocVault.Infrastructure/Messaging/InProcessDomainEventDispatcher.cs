using DocVault.Application.Abstractions.Messaging;

namespace DocVault.Infrastructure.Messaging;

public sealed class InProcessDomainEventDispatcher : IDomainEventDispatcher
{
  private readonly IServiceProvider _provider;

  public InProcessDomainEventDispatcher(IServiceProvider provider)
  {
    _provider = provider;
  }

  public async Task DispatchAsync(IEnumerable<object> domainEvents, CancellationToken cancellationToken = default)
  {
    foreach (var domainEvent in domainEvents)
    {
      var handlerType = typeof(IEventHandler<>).MakeGenericType(domainEvent.GetType());
      var enumerableType = typeof(IEnumerable<>).MakeGenericType(handlerType);
      if (_provider.GetService(enumerableType) is not IEnumerable<object> handlers)
      {
        continue;
      }

      foreach (var handler in handlers)
      {
        var method = handlerType.GetMethod("HandleAsync");
        if (method?.Invoke(handler, new[] { domainEvent, cancellationToken }) is Task task)
        {
          await task.ConfigureAwait(false);
        }
      }
    }
  }
}
