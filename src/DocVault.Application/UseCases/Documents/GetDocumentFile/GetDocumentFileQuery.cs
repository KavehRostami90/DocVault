using DocVault.Domain.Documents;

namespace DocVault.Application.UseCases.Documents.GetDocumentFile;

public sealed record GetDocumentFileQuery(DocumentId Id, Guid? CallerId = null, bool IsAdmin = false);
