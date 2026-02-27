using DocVault.Domain.Documents;
using DocVault.Application.Common.Paging;

namespace DocVault.Application.Abstractions.Persistence;

public interface IDocumentRepository
{
  Task<Document?> GetAsync(DocumentId id, CancellationToken cancellationToken = default);
  Task AddAsync(Document document, CancellationToken cancellationToken = default);
  Task UpdateAsync(Document document, CancellationToken cancellationToken = default);
  Task DeleteAsync(Document document, CancellationToken cancellationToken = default);
  Task<Page<Document>> ListAsync(PageRequest request, CancellationToken cancellationToken = default);
}
