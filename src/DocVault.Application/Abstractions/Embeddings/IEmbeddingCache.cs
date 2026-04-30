namespace DocVault.Application.Abstractions.Embeddings;

/// <summary>
/// A read-through/write-through cache for vector embeddings keyed by stable string identifiers.
/// </summary>
public interface IEmbeddingCache
{
  /// <summary>
  /// Retrieves cached embeddings for the supplied keys.
  /// Keys absent from the cache are omitted from the returned dictionary.
  /// </summary>
  Task<IReadOnlyDictionary<string, float[]>> GetManyAsync(
    IEnumerable<string> keys,
    CancellationToken cancellationToken = default);

  /// <summary>
  /// Stores a batch of embedding vectors into the cache.
  /// </summary>
  Task SetManyAsync(
    IReadOnlyDictionary<string, float[]> entries,
    CancellationToken cancellationToken = default);
}
