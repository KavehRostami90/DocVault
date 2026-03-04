namespace DocVault.Api.Contracts.Documents;

/// <summary>
/// Request model for listing documents with paging and filtering capabilities
/// </summary>
public sealed record ListDocumentsRequest(
  int Page = 1,
  int Size = 20,
  string? Sort = null,
  bool Desc = false,
  string? Title = null,
  string? Status = null,
  string? Tag = null);
