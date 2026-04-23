using DocVault.Application.Abstractions.Persistence;
using DocVault.Domain.Documents;
using DocVault.Domain.Documents.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace DocVault.Infrastructure.Persistence.Repositories;

public sealed class EfDocumentChunkRepository : IDocumentChunkRepository
{
  private readonly DocVaultDbContext _db;

  public EfDocumentChunkRepository(DocVaultDbContext db) => _db = db;

  /// <summary>
  /// Replaces all chunks for a document atomically via the EF change tracker.
  /// No SaveChanges is called here — the caller (IndexingWorker via IUnitOfWork) commits the unit of work.
  /// </summary>
  public async Task ReplaceAsync(DocumentId documentId, IReadOnlyList<DocumentChunk> chunks, CancellationToken ct = default)
  {
    var existing = await _db.DocumentChunks
      .Where(c => c.DocumentId == documentId)
      .ToListAsync(ct);

    if (existing.Count > 0)
      _db.DocumentChunks.RemoveRange(existing);

    if (chunks.Count > 0)
      await _db.DocumentChunks.AddRangeAsync(chunks, ct);
  }
}
