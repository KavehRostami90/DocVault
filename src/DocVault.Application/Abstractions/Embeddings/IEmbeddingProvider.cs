namespace DocVault.Application.Abstractions.Embeddings;

public interface IEmbeddingProvider
{
  Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default);
}
