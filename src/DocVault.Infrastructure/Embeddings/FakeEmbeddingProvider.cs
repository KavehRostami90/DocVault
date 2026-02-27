using DocVault.Application.Abstractions.Embeddings;

namespace DocVault.Infrastructure.Embeddings;

public sealed class FakeEmbeddingProvider : IEmbeddingProvider
{
  public Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default)
  {
    var vector = text.Split(' ', StringSplitOptions.RemoveEmptyEntries)
      .Select(t => (float)t.Length)
      .ToArray();
    return Task.FromResult(vector);
  }
}
