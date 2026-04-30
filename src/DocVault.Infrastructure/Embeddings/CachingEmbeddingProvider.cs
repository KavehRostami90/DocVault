using System.Security.Cryptography;
using System.Text;
using DocVault.Application.Abstractions.Embeddings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DocVault.Infrastructure.Embeddings;

/// <summary>
/// Decorator that caches embedding vectors so repeated calls for the same text
/// skip the underlying HTTP provider entirely.
/// <para>
/// <b>Partial-batch caching:</b> for a batch of N texts, only the cache-missing texts
/// are forwarded to the inner provider, reducing API calls from O(N) to O(misses).
/// </para>
/// <para>
/// Cache keys are <c>SHA-256("{modelTag}|{text}")</c>, which automatically invalidates
/// all cached entries when the embedding model is changed.
/// </para>
/// </summary>
public sealed partial class CachingEmbeddingProvider : IEmbeddingProvider
{
  private readonly IEmbeddingProvider _inner;
  private readonly IEmbeddingCache _cache;
  private readonly string _modelTag;
  private readonly bool _enabled;
  private readonly ILogger<CachingEmbeddingProvider> _logger;

  /// <param name="inner">The real embedding provider (e.g. OpenAI, Ollama, Fake).</param>
  /// <param name="cache">The backing cache store.</param>
  /// <param name="modelTag">
  /// A short identifier for the active embedding model (e.g. <c>"text-embedding-3-small"</c>).
  /// Included in the cache key so that a model switch invalidates stale entries.
  /// </param>
  /// <param name="options">Cache behaviour configuration.</param>
  /// <param name="logger">Logger.</param>
  public CachingEmbeddingProvider(
    IEmbeddingProvider inner,
    IEmbeddingCache cache,
    string modelTag,
    IOptions<EmbeddingCacheOptions> options,
    ILogger<CachingEmbeddingProvider> logger)
  {
    _inner    = inner;
    _cache    = cache;
    _modelTag = modelTag;
    _enabled  = options.Value.IsEnabled;
    _logger   = logger;
  }

  public async Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default)
  {
    var batch = await EmbedBatchAsync([text], cancellationToken);
    return batch[0];
  }

  public async Task<IReadOnlyList<float[]>> EmbedBatchAsync(
    IReadOnlyList<string> texts,
    CancellationToken cancellationToken = default)
  {
    if (texts.Count == 0)
      return [];

    if (!_enabled)
      return await _inner.EmbedBatchAsync(texts, cancellationToken);

    // Compute a stable, model-aware cache key for each input text.
    var keys   = texts.Select(ComputeKey).ToArray();
    var cached = await _cache.GetManyAsync(keys, cancellationToken);

    var missIndices = Enumerable.Range(0, texts.Count)
      .Where(i => !cached.ContainsKey(keys[i]))
      .ToList();

    LogCacheStats(_logger, texts.Count, texts.Count - missIndices.Count, missIndices.Count);

    if (missIndices.Count == 0)
      return keys.Select(k => cached[k]).ToArray();

    // Forward only uncached texts to the underlying provider.
    var missTexts   = missIndices.Select(i => texts[i]).ToList();
    var missVectors = await _inner.EmbedBatchAsync(missTexts, cancellationToken);

    // Persist new results.
    var newEntries = new Dictionary<string, float[]>(missIndices.Count);
    for (var i = 0; i < missIndices.Count; i++)
      newEntries[keys[missIndices[i]]] = missVectors[i];

    await _cache.SetManyAsync(newEntries, cancellationToken);

    // Reconstruct the result in original input order.
    return keys.Select(k =>
      cached.TryGetValue(k, out var v) ? v : newEntries[k]).ToArray();
  }

  private string ComputeKey(string text)
  {
    var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{_modelTag}|{text}"));
    return Convert.ToHexStringLower(hash);
  }

  [LoggerMessage(Level = LogLevel.Debug,
    Message = "Embedding cache — total={Total}, hits={Hits}, misses={Misses}.")]
  private static partial void LogCacheStats(ILogger logger, int total, int hits, int misses);
}
