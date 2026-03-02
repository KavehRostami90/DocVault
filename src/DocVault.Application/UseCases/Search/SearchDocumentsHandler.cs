using DocVault.Application.Abstractions.Persistence;
using DocVault.Application.Common.Paging;
using DocVault.Application.Common.Results;

namespace DocVault.Application.UseCases.Search;

public sealed class SearchDocumentsHandler
{
  private readonly IDocumentRepository _documents;

  public SearchDocumentsHandler(IDocumentRepository documents)
  {
    _documents = documents;
  }

  public async Task<Result<Page<SearchResultItem>>> HandleAsync(SearchDocumentsQuery query, CancellationToken cancellationToken = default)
  {
    var page = await _documents.SearchAsync(query.Query, query.Page, query.Size, cancellationToken);
    return Result<Page<SearchResultItem>>.Success(page);
  }
}
