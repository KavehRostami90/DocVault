namespace DocVault.Application.UseCases.Search;

public sealed record SearchDocumentsQuery(string Query, int Page = 1, int Size = 10, Guid? OwnerId = null, bool IsAdmin = false);
