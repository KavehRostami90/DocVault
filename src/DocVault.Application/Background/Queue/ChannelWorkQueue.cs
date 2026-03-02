using System.Threading.Channels;

namespace DocVault.Application.Background.Queue;

/// <summary>
/// Lock-free, async-capable work queue backed by
/// <see cref="System.Threading.Channels.Channel{T}"/>.
/// <para>
/// The unbounded channel means producers never block; backpressure is applied
/// at the application level (e.g., HTTP request limits).
/// </para>
/// </summary>
public sealed class ChannelWorkQueue<T> : IWorkQueue<T>
{
  private readonly Channel<T> _channel = Channel.CreateUnbounded<T>(
    new UnboundedChannelOptions { SingleReader = true, AllowSynchronousContinuations = false });

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
