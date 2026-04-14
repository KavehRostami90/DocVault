namespace DocVault.Application.Abstractions.Embeddings;

/// <summary>
/// Defines a service that generates vector embeddings from input text for use in semantic search or machine learning
/// scenarios.
/// </summary>
/// <remarks>Implementations of this interface may use local or remote models to generate embeddings. The returned
/// vector can be used for similarity comparisons, indexing, or other AI-driven features. Thread safety and performance
/// characteristics depend on the specific implementation.</remarks>
public interface IEmbeddingProvider
{
  /// <summary>
  /// Asynchronously generates a vector embedding for the specified text input.
  /// </summary>
  /// <param name="text">The text to embed. Cannot be null or empty.</param>
  /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
  /// <returns>A task that represents the asynchronous operation. The task result contains a float array representing the
  /// embedding vector for the input text.</returns>
  Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default);
}
