using DocVault.Application.Abstractions.Embeddings;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace DocVault.Infrastructure.Embeddings;

/// <summary>
/// In-memory embedding cache backed by a dedicated <see cref="MemoryCache"/> instance.
/// Using a dedicated instance (rather than the shared <see cref="IMemoryCache"/>)
/// enforces an isolated size limit so the embedding cache does not compete with
/// other application caches for memory.
/// </summary>
public sealed class MemoryEmbeddingCache : IEmbeddingCache, IDisposable
{
  private readonly MemoryCache _cache;
  private readonly MemoryCacheEntryOptions _entryOptions;

  public MemoryEmbeddingCache(IOptions<EmbeddingCacheOptions> options)
  {
    var opts = options.Value;

    _cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = opts.MaxEntries });

    _entryOptions = new MemoryCacheEntryOptions
    {
      SlidingExpiration = TimeSpan.FromMinutes(opts.SlidingExpirationMinutes),
      Size = 1,
    };
  }

  public Task<IReadOnlyDictionary<string, float[]>> GetManyAsync(
    IEnumerable<string> keys,
    CancellationToken cancellationToken = default)
  {
    var result = new Dictionary<string, float[]>();

    foreach (var key in keys)
    {
      if (_cache.TryGetValue(key, out float[]? vector) && vector is not null)
        result[key] = vector;
    }

    return Task.FromResult<IReadOnlyDictionary<string, float[]>>(result);
  }

  public Task SetManyAsync(
    IReadOnlyDictionary<string, float[]> entries,
    CancellationToken cancellationToken = default)
  {
    foreach (var (key, vector) in entries)
      _cache.Set(key, vector, _entryOptions);

    return Task.CompletedTask;
  }

  public void Dispose() => _cache.Dispose();
}
