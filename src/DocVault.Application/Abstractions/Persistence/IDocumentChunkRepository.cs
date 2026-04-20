using DocVault.Domain.Documents;

namespace DocVault.Application.Abstractions.Persistence;

/// <summary>Manages the chunk embeddings stored for a document. Replace-on-reindex semantics.</summary>
public interface IDocumentChunkRepository
{
    /// <summary>
    /// Deletes all existing chunks for <paramref name="documentId"/> and inserts <paramref name="chunks"/>.
    /// Idempotent: safe to call on re-index.
    /// </summary>
    Task ReplaceAsync(DocumentId documentId, IReadOnlyList<DocumentChunk> chunks, CancellationToken ct = default);
}
