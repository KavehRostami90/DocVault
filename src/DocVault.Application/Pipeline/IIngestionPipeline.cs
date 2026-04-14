namespace DocVault.Application.Pipeline;

/// <summary>
/// Abstraction for the ingestion pipeline — allows the <see cref="DocVault.Application.Background.IndexingWorker"/>
/// to depend on the contract rather than the concrete implementation (DIP).
/// </summary>
public interface IIngestionPipeline
{
  /// <summary>
  /// Runs all pipeline stages for the given file and returns the extracted text and its embedding.
  /// The caller is responsible for persisting both values to the document record.
  /// </summary>
  /// <param name="path">Relative storage path of the file to process.</param>
  /// <param name="contentType">MIME content type used to select the correct text extractor.</param>
  /// <param name="cancellationToken">Cancellation token.</param>
  /// <returns>The extracted plain text and its embedding vector.</returns>
  Task<IngestionResult> RunAsync(string path, string contentType, CancellationToken cancellationToken = default);
}
