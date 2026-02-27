using DocVault.Application.Background.Queue;
using DocVault.Application.Pipeline;

namespace DocVault.Application.Background;

public sealed class IndexingWorker
{
  private readonly IWorkQueue<(string Path, string ContentType)> _queue;
  private readonly IngestionPipeline _pipeline;

  public IndexingWorker(IWorkQueue<(string Path, string ContentType)> queue, IngestionPipeline pipeline)
  {
    _queue = queue;
    _pipeline = pipeline;
  }

  public async Task DrainAsync(CancellationToken cancellationToken = default)
  {
    while (!cancellationToken.IsCancellationRequested && _queue.TryDequeue(out var work))
    {
      var (path, contentType) = work;
      await _pipeline.RunAsync(path, contentType, cancellationToken);
    }
  }
}
