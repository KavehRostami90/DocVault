using DocVault.Domain.Documents;

namespace DocVault.Infrastructure.Search;

public sealed class HybridSearch
{
  public IReadOnlyCollection<Document> Search(IEnumerable<Document> documents, string query, float[] vector)
  {
    // Placeholder hybrid search
    return documents.ToList();
  }
}
