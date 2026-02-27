namespace DocVault.Api.Contracts.Search;

public sealed record SearchRequest(string Query, int Page = 1, int Size = 10);
