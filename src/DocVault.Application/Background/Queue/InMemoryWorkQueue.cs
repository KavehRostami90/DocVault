namespace DocVault.Application.Background.Queue;

public sealed class InMemoryWorkQueue<T> : IWorkQueue<T>
{
  private readonly Queue<T> _queue = new();
  private readonly object _gate = new();

  public void Enqueue(T workItem)
  {
    lock (_gate)
    {
      _queue.Enqueue(workItem);
    }
  }

  public bool TryDequeue(out T? workItem)
  {
    lock (_gate)
    {
      if (_queue.Count == 0)
      {
        workItem = default;
        return false;
      }
      workItem = _queue.Dequeue();
      return true;
    }
  }
}
