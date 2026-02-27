namespace DocVault.Api.Contracts.Documents;

public sealed record DocumentUpdateRequest(string Title, IReadOnlyCollection<string> Tags);
