using DocVault.Application.Common.Paging;
using DocVault.Application.UseCases.Search;
using DocVault.Domain.Documents;

namespace DocVault.Application.Abstractions.Persistence;

public interface IDocumentRepository
{
  Task<Document?> GetAsync(DocumentId id, CancellationToken cancellationToken = default);
  Task AddAsync(Document document, CancellationToken cancellationToken = default);
  Task UpdateAsync(Document document, CancellationToken cancellationToken = default);
  Task DeleteAsync(Document document, CancellationToken cancellationToken = default);
  Task<Page<Document>> ListAsync(PageRequest request, Guid? ownerId = null, CancellationToken cancellationToken = default);
  Task<Page<SearchResultItem>> SearchAsync(string query, int page, int size, Guid? ownerId = null, CancellationToken cancellationToken = default);
}
