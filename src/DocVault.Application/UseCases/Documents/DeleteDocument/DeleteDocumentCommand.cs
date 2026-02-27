using DocVault.Domain.Documents;

namespace DocVault.Application.UseCases.Documents.DeleteDocument;

public sealed record DeleteDocumentCommand(DocumentId Id);
