namespace DocVault.Api.Contracts.Search;

/// <summary>
/// Search query parameters.
/// </summary>
/// <param name="Query">Full-text query string to search for.</param>
/// <param name="Page">1-based page number to retrieve.</param>
/// <param name="Size">Maximum number of results to return per page.</param>
public sealed record SearchRequest(string Query, int Page = 1, int Size = 10);
