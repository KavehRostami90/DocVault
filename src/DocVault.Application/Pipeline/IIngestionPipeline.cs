namespace DocVault.Application.Pipeline;

/// <summary>
/// Abstraction for the ingestion pipeline — allows the <see cref="DocVault.Application.Background.IndexingWorker"/>
/// to depend on the contract rather than the concrete implementation (DIP).
/// </summary>
public interface IIngestionPipeline
{
  Task RunAsync(string path, string contentType, CancellationToken cancellationToken = default);
}
