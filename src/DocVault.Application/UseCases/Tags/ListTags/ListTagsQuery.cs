using DocVault.Application.Abstractions.Cqrs;

namespace DocVault.Application.UseCases.Tags.ListTags;

public sealed record ListTagsQuery(Guid? OwnerId = null, bool IsAdmin = false)
  : IQuery<IReadOnlyCollection<string>>;
