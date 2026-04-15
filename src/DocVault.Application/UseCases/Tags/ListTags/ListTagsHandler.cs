using DocVault.Application.Abstractions.Persistence;

namespace DocVault.Application.UseCases.Tags.ListTags;

public sealed class ListTagsHandler
{
  private readonly ITagRepository _tags;

  /// <summary>
  /// Creates a new handler for listing tag names.
  /// </summary>
  /// <param name="tags">Tag repository dependency.</param>
  public ListTagsHandler(ITagRepository tags)
  {
    _tags = tags;
  }

  /// <summary>
  /// Lists tag names for the given owner, or all tags when <paramref name="ownerId"/> is <c>null</c> (admin).
  /// </summary>
  /// <param name="ownerId">Owner to filter by, or <c>null</c> for all tags.</param>
  /// <param name="cancellationToken">Cancellation token.</param>
  /// <returns>Collection of tag names.</returns>
  public async Task<IReadOnlyCollection<string>> HandleAsync(Guid? ownerId, CancellationToken cancellationToken = default)
  {
    var items = await _tags.ListAsync(ownerId, cancellationToken);
    return items.Select(t => t.Name).ToArray();
  }
}
