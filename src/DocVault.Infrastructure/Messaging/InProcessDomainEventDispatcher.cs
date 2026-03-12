using DocVault.Application.Abstractions.Messaging;

namespace DocVault.Infrastructure.Messaging;

/// <summary>
/// Synchronous, in-process <see cref="IDomainEventDispatcher"/> that resolves
/// <see cref="IEventHandler{TEvent}"/> implementations from the DI container
/// and invokes them sequentially within the same process.
/// </summary>
public sealed class InProcessDomainEventDispatcher : IDomainEventDispatcher
{
  private readonly IServiceProvider _provider;

  /// <summary>
  /// Initialises the dispatcher with the root service provider.
  /// </summary>
  /// <param name="provider">The application <see cref="IServiceProvider"/>.</param>
  public InProcessDomainEventDispatcher(IServiceProvider provider)
  {
    _provider = provider;
  }

  /// <summary>
  /// Dispatches each domain event in <paramref name="domainEvents"/> to all registered
  /// handlers for that event type. Handlers are resolved via <see cref="IServiceProvider"/>.
  /// </summary>
  /// <param name="domainEvents">The domain events to dispatch.</param>
  /// <param name="cancellationToken">Cancellation token forwarded to each handler.</param>
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
