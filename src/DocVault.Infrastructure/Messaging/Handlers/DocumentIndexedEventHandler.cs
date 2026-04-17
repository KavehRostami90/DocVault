using DocVault.Application.Abstractions.Messaging;
using DocVault.Application.Abstractions.Realtime;
using DocVault.Domain.Events;

namespace DocVault.Infrastructure.Messaging.Handlers;

public sealed class DocumentIndexedEventHandler : IEventHandler<DocumentIndexed>
{
  private readonly IDocumentStatusBroadcaster _broadcaster;

  public DocumentIndexedEventHandler(IDocumentStatusBroadcaster broadcaster)
    => _broadcaster = broadcaster;

  public Task HandleAsync(DocumentIndexed @event, CancellationToken cancellationToken = default)
  {
    _broadcaster.Publish(@event.DocumentId.Value, "Indexed");
    return Task.CompletedTask;
  }
}
