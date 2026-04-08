using System.Threading.Channels;

namespace DocVault.Application.Background.Queue;

/// <summary>
/// Lock-free, async-capable work queue backed by
/// <see cref="System.Threading.Channels.Channel{T}"/>.
/// <para>
/// Pass a positive <paramref name="boundedCapacity"/> to apply back-pressure;
/// callers will wait when the channel is full rather than growing unboundedly.
/// A value of zero (default) creates an unbounded channel.
/// </para>
/// </summary>
public sealed class ChannelWorkQueue<T> : IWorkQueue<T>
{
  private readonly Channel<T> _channel;

  /// <summary>Creates the work queue with optional bounded capacity.</summary>
  /// <param name="boundedCapacity">
  /// Maximum items the channel will hold before blocking producers.
  /// Zero (default) means unbounded.
  /// </param>
  public ChannelWorkQueue(int boundedCapacity = 0)
  {
    _channel = boundedCapacity > 0
      ? Channel.CreateBounded<T>(new BoundedChannelOptions(boundedCapacity)
          { SingleReader = true, FullMode = BoundedChannelFullMode.Wait })
      : Channel.CreateUnbounded<T>(
          new UnboundedChannelOptions { SingleReader = true, AllowSynchronousContinuations = false });
  }

  /// <inheritdoc />
  public void Enqueue(T workItem) => _channel.Writer.TryWrite(workItem);

  /// <inheritdoc />
  public bool TryDequeue(out T? workItem) => _channel.Reader.TryRead(out workItem);

  /// <inheritdoc />
  /// <remarks>Blocks asynchronously until an item becomes available or the
  /// <paramref name="cancellationToken"/> is cancelled.</remarks>
  public ValueTask<T> DequeueAsync(CancellationToken cancellationToken = default)
    => _channel.Reader.ReadAsync(cancellationToken);
}
