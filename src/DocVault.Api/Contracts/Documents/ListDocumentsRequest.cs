namespace DocVault.Api.Contracts.Documents;

/// <summary>
/// Request model for listing documents with paging and filtering capabilities
/// </summary>
/// <param name="Page">1-based page number to retrieve.</param>
/// <param name="Size">Maximum number of documents to return.</param>
/// <param name="Sort">Optional sort field (e.g., title, status).</param>
/// <param name="Desc">True for descending sort order.</param>
/// <param name="Title">Filter by documents whose title contains this value.</param>
/// <param name="Status">Filter by processing status.</param>
/// <param name="Tag">Filter by a single tag.</param>
public sealed record ListDocumentsRequest(
  int Page = 1,
  int Size = 20,
  string? Sort = null,
  bool Desc = false,
  string? Title = null,
  string? Status = null,
  string? Tag = null);
