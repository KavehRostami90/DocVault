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
}
