using DocVault.Application.Abstractions.Messaging;
using Microsoft.Extensions.DependencyInjection;

namespace DocVault.Infrastructure.Messaging;

/// <summary>
/// Synchronous, in-process <see cref="IDomainEventDispatcher"/> that resolves
/// <see cref="IEventHandler{TEvent}"/> implementations from the DI container
/// and invokes them sequentially within the same process.
/// </summary>
/// <remarks>
/// Uses <c>MakeGenericType</c> to recover the concrete event type at runtime,
/// ensuring compile-time correctness at the DI boundary while supporting
/// polymorphic dispatch without a <c>dynamic</c> cast.
/// Handlers must be registered as <c>IEventHandler&lt;TEvent&gt;</c> in DI.
/// </remarks>
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
  /// Dispatches each domain event to all registered handlers of its concrete type.
  /// </summary>
  /// <param name="domainEvents">The domain events to dispatch.</param>
  /// <param name="cancellationToken">Cancellation token forwarded to each handler.</param>
  public async Task DispatchAsync(IEnumerable<object> domainEvents, CancellationToken cancellationToken = default)
  {
    foreach (var domainEvent in domainEvents)
      await DispatchSingleAsync(domainEvent, cancellationToken);
  }

  private async Task DispatchSingleAsync(object domainEvent, CancellationToken cancellationToken)
  {
    var eventType   = domainEvent.GetType();
    var handlerType = typeof(IEventHandler<>).MakeGenericType(eventType);
    var handleMethod = handlerType.GetMethod(nameof(IEventHandler<object>.HandleAsync))
      ?? throw new InvalidOperationException($"HandleAsync not found on {handlerType}.");

    var handlers = _provider.GetServices(handlerType);
    foreach (var handler in handlers)
    {
      var task = (Task)handleMethod.Invoke(handler, [domainEvent, cancellationToken])!;
      await task;
    }
  }
}
