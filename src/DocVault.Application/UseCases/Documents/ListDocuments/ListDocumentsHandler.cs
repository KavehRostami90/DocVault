using DocVault.Application.Abstractions.Persistence;
using DocVault.Application.Common.Paging;

namespace DocVault.Application.UseCases.Documents.ListDocuments;

public sealed class ListDocumentsHandler
{
  private readonly IDocumentRepository _documents;

  public ListDocumentsHandler(IDocumentRepository documents)
  {
    _documents = documents;
  }

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
