namespace DocVault.Application.Background.Queue;

public interface IWorkQueue<T>
{
  /// <summary>Enqueue an item; always succeeds immediately.</summary>
  void Enqueue(T workItem);

  /// <summary>Non-blocking dequeue; returns false when empty.</summary>
  bool TryDequeue(out T? workItem);

  /// <summary>Async-blocking dequeue; awaits until an item is available.</summary>
  ValueTask<T> DequeueAsync(CancellationToken cancellationToken = default);
}
