namespace DocVault.Api.Contracts.Documents;

public sealed record DocumentCreateRequest(string Title, string FileName, string ContentType, long Size, IReadOnlyCollection<string> Tags);
