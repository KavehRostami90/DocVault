using DocVault.Application.Common.Paging;
using DocVault.Application.UseCases.Search;
using DocVault.Domain.Documents;

namespace DocVault.Application.Abstractions.Persistence;

public interface IDocumentRepository
{
  /// <summary>Returns the document with the given <paramref name="id"/>, or <c>null</c> if not found.</summary>
  Task<Document?> GetAsync(DocumentId id, CancellationToken cancellationToken = default);

  /// <summary>Persists a new document to the store.</summary>
  Task AddAsync(Document document, CancellationToken cancellationToken = default);

  /// <summary>Saves changes to an existing document.</summary>
  Task UpdateAsync(Document document, CancellationToken cancellationToken = default);

  /// <summary>Removes a document from the store.</summary>
  Task DeleteAsync(Document document, CancellationToken cancellationToken = default);

  /// <summary>
  /// Returns a paginated list of documents, optionally scoped to a single owner.
  /// Pass <paramref name="ownerId"/> as <c>null</c> to list documents for all users (admin use).
  /// </summary>
  Task<Page<Document>> ListAsync(PageRequest request, Guid? ownerId = null, CancellationToken cancellationToken = default);

  /// <summary>
  /// Performs a full-text search and returns ranked result items,
  /// optionally scoped to a single owner.
  /// </summary>
  Task<Page<SearchResultItem>> SearchAsync(string query, int page, int size, Guid? ownerId = null, CancellationToken cancellationToken = default);

  /// <summary>
  /// Returns the number of documents in each processing status,
  /// keyed by status name (e.g. <c>"Pending"</c>, <c>"Indexed"</c>).
  /// </summary>
  Task<Dictionary<string, long>> GetCountsByStatusAsync(CancellationToken cancellationToken = default);
}
