namespace DocVault.Application.Background.Queue;

public interface IWorkQueue<T>
{
  void Enqueue(T workItem);
  bool TryDequeue(out T? workItem);
}
