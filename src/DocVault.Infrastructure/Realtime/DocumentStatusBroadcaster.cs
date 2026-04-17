using System.Collections.Concurrent;
using System.Threading.Channels;
using DocVault.Application.Abstractions.Realtime;

namespace DocVault.Infrastructure.Realtime;

/// <summary>
/// Thread-safe singleton broadcaster for real-time document status events.
/// SSE endpoints subscribe per-document and receive events via their own channel.
/// </summary>
public sealed class DocumentStatusBroadcaster : IDocumentStatusBroadcaster
{
  private readonly ConcurrentDictionary<Guid, List<Channel<DocumentStatusEvent>>> _subscriptions = new();
  private readonly object _sync = new();

  public Channel<DocumentStatusEvent> Subscribe(Guid documentId)
  {
    var channel = Channel.CreateUnbounded<DocumentStatusEvent>(
      new UnboundedChannelOptions { SingleWriter = true, SingleReader = true });

    lock (_sync)
    {
      _subscriptions.AddOrUpdate(
        documentId,
        _ => [channel],
        (_, list) => { list.Add(channel); return list; });
    }

    return channel;
  }

  public void Unsubscribe(Guid documentId, Channel<DocumentStatusEvent> channel)
  {
    lock (_sync)
    {
      if (!_subscriptions.TryGetValue(documentId, out var list)) return;
      list.Remove(channel);
      if (list.Count == 0) _subscriptions.TryRemove(documentId, out _);
    }
  }

  public void Publish(Guid documentId, string status, string? error = null)
  {
    if (!_subscriptions.TryGetValue(documentId, out var list)) return;

    List<Channel<DocumentStatusEvent>> snapshot;
    lock (_sync) { snapshot = [.. list]; }

    var evt = new DocumentStatusEvent(documentId, status, error);
    foreach (var channel in snapshot)
      channel.Writer.TryWrite(evt);
  }
}
