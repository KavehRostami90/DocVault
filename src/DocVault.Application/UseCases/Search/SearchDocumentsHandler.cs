using DocVault.Application.Common.Results;
using DocVault.Domain.Documents;

namespace DocVault.Application.UseCases.Search;

public sealed class SearchDocumentsHandler
{
  public Task<Result<IReadOnlyCollection<Document>>> HandleAsync(SearchDocumentsQuery query, CancellationToken cancellationToken = default)
  {
    IReadOnlyCollection<Document> empty = Array.Empty<Document>();
    return Task.FromResult(Result<IReadOnlyCollection<Document>>.Success(empty));
  }
}
