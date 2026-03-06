using DocVault.Application.Abstractions.Persistence;
using DocVault.Application.Common.Paging;

namespace DocVault.Application.UseCases.Documents.ListDocuments;

/// <summary>
/// Handles listing documents with paging, sorting, and filtering.
/// </summary>
public sealed class ListDocumentsHandler
{
  private readonly IDocumentRepository _documents;

  /// <summary>
  /// Creates a new handler for listing documents.
  /// </summary>
  /// <param name="documents">Document repository.</param>
  public ListDocumentsHandler(IDocumentRepository documents)
  {
    _documents = documents;
  }

  /// <summary>
  /// Lists documents matching the provided query.
  /// </summary>
  /// <param name="query">Paging and filter query.</param>
  /// <param name="cancellationToken">Cancellation token.</param>
  /// <returns>Paged list of documents.</returns>
  public Task<Page<DocVault.Domain.Documents.Document>> HandleAsync(ListDocumentsQuery query, CancellationToken cancellationToken = default)
  {
    var filters = new Dictionary<string, string?>
    {
      ["title"] = query.Title,
      ["status"] = query.Status,
      ["tag"] = query.Tag
    };

    var request = new PageRequest(query.Page, query.Size, query.Sort, query.Desc, filters);
    return _documents.ListAsync(request, cancellationToken);
  }
}
