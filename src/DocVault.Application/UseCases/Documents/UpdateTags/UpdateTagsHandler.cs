using DocVault.Application.Abstractions.Persistence;
using DocVault.Application.Common.Results;
using DocVault.Domain.Documents;
using DocVault.Domain.Extensions;

namespace DocVault.Application.UseCases.Documents.UpdateTags;

public sealed class UpdateTagsHandler
{
  private readonly IDocumentRepository _documents;
  private readonly ITagRepository _tags;

  public UpdateTagsHandler(IDocumentRepository documents, ITagRepository tags)
  {
    _documents = documents;
    _tags = tags;
  }

  public async Task<Result> HandleAsync(UpdateTagsCommand command, CancellationToken cancellationToken = default)
  {
    var doc = await _documents.GetAsync(command.Id, cancellationToken);
    if (doc is null)
    {
      return Result.Failure(Errors.NotFound);
    }

    if (doc.IsFailed())
    {
      return Result.Failure(Errors.Conflict);
    }

    var tagEntities = new List<Tag>();
    foreach (var name in command.Tags)
    {
      var existing = await _tags.GetByNameAsync(name, cancellationToken);
      if (existing is null)
      {
        var tag = new Tag(Guid.NewGuid(), name);
        await _tags.AddAsync(tag, cancellationToken);
        tagEntities.Add(tag);
      }
      else
      {
        tagEntities.Add(existing);
      }
    }

    doc.ReplaceTags(tagEntities);
    await _documents.UpdateAsync(doc, cancellationToken);
    return Result.Success();
  }
}
