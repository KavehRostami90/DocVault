using DocVault.Domain.Documents;

namespace DocVault.Infrastructure.Search;

public sealed class KeywordSearch
{
  public IReadOnlyCollection<Document> Search(IEnumerable<Document> documents, string query)
  {
    var matches = documents.Where(d => d.Title.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
    return matches;
  }
}
