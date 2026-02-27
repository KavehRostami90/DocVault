using DocVault.Domain.Documents;

namespace DocVault.Application.Abstractions.Persistence;

public interface ITagRepository
{
  Task<Tag?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
  Task<IReadOnlyCollection<Tag>> GetByNamesAsync(IEnumerable<string> names, CancellationToken cancellationToken = default);
  Task AddAsync(Tag tag, CancellationToken cancellationToken = default);
}
