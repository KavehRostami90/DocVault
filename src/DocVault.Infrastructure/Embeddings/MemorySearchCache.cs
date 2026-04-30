using DocVault.Application.Abstractions.Embeddings;
using DocVault.Application.Common.Paging;
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
    private readonly SearchCacheOptions _options;

    public MemorySearchCache(IOptions<SearchCacheOptions> options)
    {
        _options = options.Value;
        _cache   = new MemoryCache(new MemoryCacheOptions { SizeLimit = _options.MaxEntries });
    }

    public Task<Page<SearchResultItem>?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        _cache.TryGetValue(key, out Page<SearchResultItem>? page);
        return Task.FromResult(page);
    }

    public Task SetAsync(string key, Page<SearchResultItem> page, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        var entryOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = ttl,
            Size = 1,
        };
        _cache.Set(key, page, entryOptions);
        return Task.CompletedTask;
    }

    public void Dispose() => _cache.Dispose();
}
