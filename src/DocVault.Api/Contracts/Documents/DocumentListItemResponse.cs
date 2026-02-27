namespace DocVault.Api.Contracts.Documents;

public sealed record DocumentListItemResponse(Guid Id, string Title, string FileName, string Status);
