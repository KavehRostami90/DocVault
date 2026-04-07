using DocVault.Application.Abstractions.Persistence;
using DocVault.Application.Common.Paging;
using DocVault.Application.Common.Results;

namespace DocVault.Application.UseCases.Search;

/// <summary>
/// Handles full-text search across documents.
/// </summary>
public sealed class SearchDocumentsHandler
{
  private readonly IDocumentRepository _documents;

  /// <summary>
  /// Creates a new handler for searching documents.
  /// </summary>
  /// <param name="documents">Document repository.</param>
  public SearchDocumentsHandler(IDocumentRepository documents)
  {
    _documents = documents;
  }

  /// <summary>
  /// Executes a search query and returns a paged result.
  /// </summary>
  /// <param name="query">Search query.</param>
  /// <param name="cancellationToken">Cancellation token.</param>
  /// <returns>Result containing the page of search results.</returns>
  public async Task<Result<Page<SearchResultItem>>> HandleAsync(SearchDocumentsQuery query, CancellationToken cancellationToken = default)
  {
    var ownerId = query.IsAdmin ? null : query.OwnerId;
    var page = await _documents.SearchAsync(query.Query, query.Page, query.Size, ownerId, cancellationToken);
    return Result<Page<SearchResultItem>>.Success(page);
  }
}
