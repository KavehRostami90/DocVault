using System.Threading.Channels;
using DocVault.Domain.Documents;

namespace DocVault.Application.Abstractions.Realtime;

public interface IDocumentStatusBroadcaster
{
  ChannelReader<DocumentStatusEvent> Subscribe(Guid documentId);
  void Unsubscribe(Guid documentId, ChannelReader<DocumentStatusEvent> reader);
  void Publish(Guid documentId, DocumentStatus status, string? error = null);
}
