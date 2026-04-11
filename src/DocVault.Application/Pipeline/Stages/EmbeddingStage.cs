using DocVault.Application.Abstractions.Embeddings;

namespace DocVault.Application.Pipeline.Stages;

public sealed class EmbeddingStage
{
  private readonly IEmbeddingProvider _provider;

  public EmbeddingStage(IEmbeddingProvider provider)
  {
    _provider = provider;
  }
  /// <summary>
  /// Generates an embedding vector for the given text using the configured
  /// </summary>
  /// <param name="text"></param>
  /// <param name="cancellationToken"></param>
  /// <returns></returns>
  public Task<float[]> GenerateAsync(string text, CancellationToken cancellationToken = default)
    => _provider.EmbedAsync(text, cancellationToken);
}
