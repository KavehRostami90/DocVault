namespace DocVault.Application.Background.Queue;

/// <summary>
/// Kept for backward-compatibility and unit-test scenarios where async
/// blocking is not required.  Production code should use
/// <see cref="ChannelWorkQueue{T}"/>.
/// </summary>
public sealed class InMemoryWorkQueue<T> : IWorkQueue<T>
{
  private readonly Queue<T> _queue = new();
  private readonly object _gate = new();

  public void Enqueue(T workItem)
  {
    lock (_gate)
      _queue.Enqueue(workItem);
  }

  public bool TryDequeue(out T? workItem)
  {
    lock (_gate)
    {
      if (_queue.Count == 0) { workItem = default; return false; }
      workItem = _queue.Dequeue();
      return true;
    }
  }

  /// <inheritdoc />
  /// <remarks>
  /// InMemoryWorkQueue does not support async blocking; it returns
  /// immediately if the queue is empty.  Use <see cref="ChannelWorkQueue{T}"/>
  /// when async blocking is needed.
  /// </remarks>
  public ValueTask<T> DequeueAsync(CancellationToken cancellationToken = default)
  {
    if (TryDequeue(out var item))
      return ValueTask.FromResult(item!);

    return ValueTask.FromException<T>(
      new InvalidOperationException(
        $"{nameof(InMemoryWorkQueue<T>)} does not support async waiting. Use ChannelWorkQueue instead."));
  }
}
