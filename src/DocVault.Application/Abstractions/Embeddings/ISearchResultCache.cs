using DocVault.Application.Common.Paging;
using DocVault.Application.UseCases.Search;

namespace DocVault.Application.Abstractions.Embeddings;

/// <summary>
/// Short-lived cache for search result pages, keyed by a normalised query fingerprint.
/// Reduces repeated embedding calls and database load for popular or repeated queries.
/// </summary>
public interface ISearchResultCache
{
    /// <summary>Returns a cached page, or <c>null</c> if the key is absent or expired.</summary>
    Task<Page<SearchResultItem>?> GetAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Stores a result page under <paramref name="key"/> with the given <paramref name="ttl"/>.</summary>
    Task SetAsync(string key, Page<SearchResultItem> page, TimeSpan ttl, CancellationToken cancellationToken = default);
}
