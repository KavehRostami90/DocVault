using DocVault.Application.Abstractions.Embeddings;

namespace DocVault.Application.Pipeline.Stages;

public sealed class EmbeddingStage
{
  private readonly IEmbeddingProvider _provider;

  public EmbeddingStage(IEmbeddingProvider provider)
  {
    _provider = provider;
  }

  public Task<float[]> GenerateAsync(string text, CancellationToken cancellationToken = default)
    => _provider.EmbedAsync(text, cancellationToken);

  /// <summary>
  /// Generates embeddings for all chunks in a single batch request.
  /// Reduces HTTP round-trips from O(n) to O(1) for providers that support batching.
  /// </summary>
  public Task<IReadOnlyList<float[]>> GenerateBatchAsync(
    IReadOnlyList<string> texts,
    CancellationToken cancellationToken = default)
    => _provider.EmbedBatchAsync(texts, cancellationToken);
}
