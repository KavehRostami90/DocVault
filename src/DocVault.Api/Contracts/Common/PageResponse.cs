namespace DocVault.Api.Contracts.Common;

/// <summary>
/// Envelope for paged responses.
/// </summary>
/// <param name="Items">Items contained in the current page.</param>
/// <param name="Page">1-based page number returned.</param>
/// <param name="Size">Page size applied to the query.</param>
/// <param name="TotalCount">Total number of matching items across all pages.</param>
public sealed record PageResponse<T>(IReadOnlyCollection<T> Items, int Page, int Size, long TotalCount);
