namespace DocVault.Api.Contracts.Documents;

public sealed record DocumentReadResponse(Guid Id, string Title, string FileName, string ContentType, long Size, string Status, IReadOnlyCollection<string> Tags);
