using System.Threading.Channels;

namespace DocVault.Application.Abstractions.Realtime;

public interface IDocumentStatusBroadcaster
{
  Channel<DocumentStatusEvent> Subscribe(Guid documentId);
  void Unsubscribe(Guid documentId, Channel<DocumentStatusEvent> channel);
  void Publish(Guid documentId, string status, string? error = null);
}
