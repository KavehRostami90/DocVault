using DocVault.Domain.Documents;

namespace DocVault.Infrastructure.Search;

public sealed class VectorSearch
{
  public IReadOnlyCollection<Document> Search(IEnumerable<Document> documents, float[] vector)
  {
    // Placeholder vector search
    return documents.ToList();
  }
}
