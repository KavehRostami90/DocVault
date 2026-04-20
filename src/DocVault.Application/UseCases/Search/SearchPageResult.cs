using DocVault.Application.Common.Paging;

namespace DocVault.Application.UseCases.Search;

/// <summary>
/// Wraps a paged search result with metadata about which search strategy was used.
/// </summary>
public sealed record SearchPageResult(Page<SearchResultItem> Page, SearchMode Mode);
