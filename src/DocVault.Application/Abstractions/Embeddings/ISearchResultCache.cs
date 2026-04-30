using DocVault.Application.UseCases.Search;

namespace DocVault.Application.Abstractions.Embeddings;

/// <summary>
/// Short-lived cache for search result pages, keyed by a normalised query fingerprint.
/// Reduces repeated embedding calls and database load for popular or repeated queries.
/// </summary>
public interface ISearchResultCache
{
    /// <summary>Returns a cached search result, or <c>null</c> if the key is absent or expired.</summary>
    Task<SearchPageResult?> GetAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Stores a search result under <paramref name="key"/>.</summary>
    Task SetAsync(string key, SearchPageResult result, CancellationToken cancellationToken = default);
}
