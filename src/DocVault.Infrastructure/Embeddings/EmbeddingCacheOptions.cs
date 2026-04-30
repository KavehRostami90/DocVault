namespace DocVault.Infrastructure.Embeddings;

/// <summary>
/// Configuration for the in-memory embedding vector cache.
/// </summary>
public sealed class EmbeddingCacheOptions
{
  public const string Section = "EmbeddingCache";

  /// <summary>
  /// Enables or disables the cache. Default: <c>true</c>.
  /// Set to <c>false</c> to always call the underlying provider (useful for debugging).
  /// </summary>
  public bool IsEnabled { get; init; } = true;

  /// <summary>
  /// Maximum number of embedding vectors to keep in memory.
  /// Each 768-dimensional <c>float[]</c> occupies roughly 3 KB, so the default of
  /// 10,000 entries uses approximately 30 MB of RAM.
  /// </summary>
  public int MaxEntries { get; init; } = 10_000;

  /// <summary>
  /// Sliding expiration window in minutes. An entry is evicted if it has not been
  /// accessed within this period. Default: 60 minutes.
  /// </summary>
  public int SlidingExpirationMinutes { get; init; } = 60;
}
