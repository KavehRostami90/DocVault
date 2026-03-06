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
  /// Lists all tag names ordered by name.
  /// </summary>
  /// <param name="cancellationToken">Cancellation token.</param>
  /// <returns>Collection of tag names.</returns>
  public async Task<IReadOnlyCollection<string>> HandleAsync(CancellationToken cancellationToken = default)
  {
    var items = await _tags.ListAsync(cancellationToken);
    return items.Select(t => t.Name).ToArray();
  }
}
