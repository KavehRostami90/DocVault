namespace DocVault.Application.UseCases.Imports.GetImportStatus;

public sealed record GetImportStatusQuery(Guid Id, Guid? CallerId = null, bool IsAdmin = false);
