using DocVault.Application.Abstractions.Persistence;
using DocVault.Application.Common.Results;
using DocVault.Domain.Documents;
using DocVault.Domain.Extensions;

namespace DocVault.Application.UseCases.Documents.UpdateTags;

/// <summary>
/// Handles replacing the tag set for a document.
/// </summary>
public sealed class UpdateTagsHandler
{
  private readonly IDocumentRepository _documents;
  private readonly ITagRepository _tags;

  /// <summary>
  /// Creates a new handler for updating document tags.
  /// </summary>
  /// <param name="documents">Document repository.</param>
  /// <param name="tags">Tag repository.</param>
  public UpdateTagsHandler(IDocumentRepository documents, ITagRepository tags)
  {
    _documents = documents;
    _tags = tags;
  }

  /// <summary>
  /// Replaces tags on the given document.
  /// </summary>
  /// <param name="command">Update tags command.</param>
  /// <param name="cancellationToken">Cancellation token.</param>
  /// <returns>Result indicating success or conflict/not found.</returns>
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
