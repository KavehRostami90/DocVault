using DocVault.Application.Abstractions.Persistence;

namespace DocVault.Application.UseCases.Tags.ListTags;

public sealed class ListTagsHandler
{
  private readonly ITagRepository _tags;

  public ListTagsHandler(ITagRepository tags)
  {
    _tags = tags;
  }

  public async Task<IReadOnlyCollection<string>> HandleAsync(CancellationToken cancellationToken = default)
  {
    var items = await _tags.ListAsync(cancellationToken);
    return items.Select(t => t.Name).ToArray();
  }
}
