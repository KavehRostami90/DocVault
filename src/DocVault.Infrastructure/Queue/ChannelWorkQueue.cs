using System.Threading.Channels;
using DocVault.Application.Background.Queue;

namespace DocVault.Infrastructure.Queue;

/// <summary>
/// Lock-free, async-capable work queue backed by <see cref="System.Threading.Channels.Channel{T}"/>.
/// Pass a positive <paramref name="boundedCapacity"/> to apply back-pressure;
/// callers will wait when the channel is full. Zero (default) creates an unbounded channel.
/// </summary>
public sealed class ChannelWorkQueue<T> : IWorkQueue<T>
{
  private readonly Channel<T> _channel;

  public ChannelWorkQueue(int boundedCapacity = 0)
  {
    _channel = boundedCapacity > 0
      ? Channel.CreateBounded<T>(new BoundedChannelOptions(boundedCapacity)
          { SingleReader = true, FullMode = BoundedChannelFullMode.Wait })
      : Channel.CreateUnbounded<T>(
          new UnboundedChannelOptions { SingleReader = true, AllowSynchronousContinuations = false });
  }

  public void Enqueue(T workItem) => _channel.Writer.TryWrite(workItem);

  public bool TryDequeue(out T? workItem) => _channel.Reader.TryRead(out workItem);

  public ValueTask<T> DequeueAsync(CancellationToken cancellationToken = default)
    => _channel.Reader.ReadAsync(cancellationToken);
}
