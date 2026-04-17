using System.Threading.Channels;
using DocVault.Application.Abstractions.Realtime;
using DocVault.Domain.Documents;

namespace DocVault.Infrastructure.Realtime;

/// <summary>
/// Thread-safe singleton broadcaster for real-time document status events.
/// SSE endpoints subscribe per-document and receive events via a <see cref="ChannelReader{T}"/>.
/// The broadcaster owns the channel writers; <see cref="Unsubscribe"/> completes them so
/// <c>ReadAllAsync</c> terminates cleanly without the caller needing write access.
/// </summary>
public sealed class DocumentStatusBroadcaster : IDocumentStatusBroadcaster
{
  // Keyed by document ID; each entry is a list of active channels for that document.
  private readonly Dictionary<Guid, List<Channel<DocumentStatusEvent>>> _subscriptions = new();
  // Maps the reader back to its owning channel for O(1) unsubscribe lookup.
  private readonly Dictionary<ChannelReader<DocumentStatusEvent>, (Guid DocId, Channel<DocumentStatusEvent> Channel)> _readerIndex = new();
  private readonly object _sync = new();

  public ChannelReader<DocumentStatusEvent> Subscribe(Guid documentId)
  {
    var channel = Channel.CreateUnbounded<DocumentStatusEvent>(
      new UnboundedChannelOptions { SingleWriter = false, SingleReader = true });

    lock (_sync)
    {
      if (!_subscriptions.TryGetValue(documentId, out var list))
      {
        list = new List<Channel<DocumentStatusEvent>>();
        _subscriptions[documentId] = list;
      }
      list.Add(channel);
      _readerIndex[channel.Reader] = (documentId, channel);
    }

    return channel.Reader;
  }

  public void Unsubscribe(Guid documentId, ChannelReader<DocumentStatusEvent> reader)
  {
    Channel<DocumentStatusEvent>? channel = null;

    lock (_sync)
    {
      if (!_readerIndex.Remove(reader, out var entry)) return;
      channel = entry.Channel;

      if (_subscriptions.TryGetValue(documentId, out var list))
      {
        list.Remove(channel);
        if (list.Count == 0) _subscriptions.Remove(documentId);
      }
    }

    channel?.Writer.TryComplete();
  }

  public void Publish(Guid documentId, DocumentStatus status, string? error = null)
  {
    List<Channel<DocumentStatusEvent>> snapshot;

    lock (_sync)
    {
      if (!_subscriptions.TryGetValue(documentId, out var list)) return;
      snapshot = [.. list];
    }

    var evt = new DocumentStatusEvent(documentId, status, error);
    foreach (var channel in snapshot)
      channel.Writer.TryWrite(evt);
  }
}
