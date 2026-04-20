using DocVault.Application.Abstractions.Persistence;
using DocVault.Domain.Documents;
using DocVault.Domain.Documents.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace DocVault.Infrastructure.Persistence.Repositories;

public sealed class EfDocumentChunkRepository : IDocumentChunkRepository
{
    private readonly DocVaultDbContext _db;

    public EfDocumentChunkRepository(DocVaultDbContext db) => _db = db;

    public async Task ReplaceAsync(DocumentId documentId, IReadOnlyList<DocumentChunk> chunks, CancellationToken ct = default)
    {
        // Delete stale chunks first (idempotent on re-index).
        await _db.DocumentChunks
            .Where(c => c.DocumentId == documentId)
            .ExecuteDeleteAsync(ct);

        if (chunks.Count == 0)
            return;

        await _db.DocumentChunks.AddRangeAsync(chunks, ct);
        await _db.SaveChangesAsync(ct);
    }
}
