using DocVault.Application.Abstractions.Cqrs;
using DocVault.Application.Abstractions.Persistence;

namespace DocVault.Application.UseCases.Tags.ListTags;

public sealed class ListTagsHandler : IQueryHandler<ListTagsQuery, IReadOnlyCollection<string>>
{
  private readonly ITagRepository _tags;

  public ListTagsHandler(ITagRepository tags)
  {
    _tags = tags;
  }

  public async Task<IReadOnlyCollection<string>> HandleAsync(ListTagsQuery query, CancellationToken cancellationToken = default)
  {
    var ownerId = query.IsAdmin ? null : query.OwnerId;
    var items = await _tags.ListAsync(ownerId, cancellationToken);
    return items.Select(t => t.Name).ToArray();
  }
}
