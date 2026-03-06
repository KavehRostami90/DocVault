namespace DocVault.Api.Contracts.Common;

/// <summary>
/// Paging, sorting, and filtering parameters for list endpoints.
/// </summary>
/// <param name="Page">1-based page number to retrieve.</param>
/// <param name="Size">Maximum number of items to return in a single page.</param>
/// <param name="Sort">Optional sort instructions for the result set.</param>
/// <param name="Filter">Optional filter criteria to narrow the results.</param>
public sealed record PageRequest(int Page = 1, int Size = 20, SortSpec? Sort = null, FilterSpec? Filter = null);
