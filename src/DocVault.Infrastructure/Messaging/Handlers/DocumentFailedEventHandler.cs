using DocVault.Application.Abstractions.Messaging;
using DocVault.Application.Abstractions.Realtime;
using DocVault.Domain.Events;

namespace DocVault.Infrastructure.Messaging.Handlers;

public sealed class DocumentFailedEventHandler : IEventHandler<DocumentFailed>
{
  private readonly IDocumentStatusBroadcaster _broadcaster;

  public DocumentFailedEventHandler(IDocumentStatusBroadcaster broadcaster)
    => _broadcaster = broadcaster;

  public Task HandleAsync(DocumentFailed @event, CancellationToken cancellationToken = default)
  {
    _broadcaster.Publish(@event.DocumentId.Value, "Failed", @event.Error);
    return Task.CompletedTask;
  }
}
