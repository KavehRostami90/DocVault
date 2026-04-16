using DocVault.Domain.Documents;

namespace DocVault.Application.Abstractions.Persistence;

/// <summary>
/// Abstraction for persisting and querying tags.
/// </summary>
public interface ITagRepository
{
  /// <summary>Gets a tag by name.</summary>
  Task<Tag?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
  /// <summary>Gets tags matching the provided names.</summary>
  Task<IReadOnlyCollection<Tag>> GetByNamesAsync(IEnumerable<string> names, CancellationToken cancellationToken = default);
  /// <summary>Lists all tags ordered by name. When <paramref name="ownerId"/> is provided, only tags used in that user's documents are returned.</summary>
  Task<IReadOnlyCollection<Tag>> ListAsync(Guid? ownerId = null, CancellationToken cancellationToken = default);
  /// <summary>Adds a new tag.</summary>
  Task AddAsync(Tag tag, CancellationToken cancellationToken = default);
}
