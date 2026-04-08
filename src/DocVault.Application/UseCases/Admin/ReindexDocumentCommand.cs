using DocVault.Domain.Documents;

namespace DocVault.Application.UseCases.Admin;

/// <summary>
/// Command that re-queues an existing document for the full ingestion pipeline.
/// </summary>
/// <param name="DocumentId">Identifier of the document to reindex.</param>
public sealed record ReindexDocumentCommand(DocumentId DocumentId);
