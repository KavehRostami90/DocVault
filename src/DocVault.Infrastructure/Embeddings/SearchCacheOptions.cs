namespace DocVault.Infrastructure.Embeddings;

/// <summary>
/// Configuration for the in-memory search result cache.
/// </summary>
public sealed class SearchCacheOptions
{
    public const string Section = "SearchCache";

    /// <summary>
    /// Enables or disables the search result cache. Default: <c>true</c>.
    /// </summary>
    public bool IsEnabled { get; init; } = true;

    /// <summary>
    /// Absolute TTL for cached search pages, in seconds. Default: 120 (2 minutes).
    /// Short TTLs prevent stale results after document ingestion.
    /// </summary>
    public int AbsoluteExpirationSeconds { get; init; } = 120;

    /// <summary>
    /// Maximum number of search result pages to keep in memory. Default: 1 000.
    /// </summary>
    public int MaxEntries { get; init; } = 1_000;
}
