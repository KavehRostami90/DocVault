namespace DocVault.Api.Contracts.Search;

public sealed record SearchResultItemResponse(Guid Id, string Title, string Snippet, double Score);
