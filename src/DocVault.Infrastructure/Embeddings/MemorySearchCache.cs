using DocVault.Application.Abstractions.Embeddings;
using DocVault.Application.UseCases.Search;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace DocVault.Infrastructure.Embeddings;

/// <summary>
/// In-memory search result cache backed by a dedicated <see cref="MemoryCache"/> instance.
/// A dedicated instance enforces an isolated size limit so this cache does not compete
/// with other application caches for memory.
/// </summary>
public sealed class MemorySearchCache : ISearchResultCache, IDisposable
{
    private readonly MemoryCache _cache;
    private readonly bool _isEnabled;
    private readonly TimeSpan _ttl;

    public MemorySearchCache(IOptions<SearchCacheOptions> options)
    {
        var configured = options.Value;

        _isEnabled = configured.IsEnabled;
        _ttl = TimeSpan.FromSeconds(Math.Max(1, configured.AbsoluteExpirationSeconds));

        _cache = new MemoryCache(new MemoryCacheOptions
        {
            SizeLimit = Math.Max(1, configured.MaxEntries)
        });
    }

    public Task<SearchPageResult?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        if (!_isEnabled)
            return Task.FromResult<SearchPageResult?>(null);

        _cache.TryGetValue(key, out SearchPageResult? result);
        return Task.FromResult(result);
    }

    public Task SetAsync(string key, SearchPageResult result, CancellationToken cancellationToken = default)
    {
        if (!_isEnabled)
            return Task.CompletedTask;

        var entryOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = _ttl,
            Size = 1,
        };
        _cache.Set(key, result, entryOptions);
        return Task.CompletedTask;
    }

    public void Dispose() => _cache.Dispose();
}
