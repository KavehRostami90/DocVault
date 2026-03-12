using DocVault.Domain.Documents;

namespace DocVault.Application.UseCases.Imports.StartImportJob;

public sealed record StartImportJobCommand(DocumentId DocumentId, string FileName, string StoragePath, string ContentType);
