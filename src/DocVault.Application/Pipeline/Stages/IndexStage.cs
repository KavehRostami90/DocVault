namespace DocVault.Application.Pipeline.Stages;

/// <summary>
/// Final stage of the ingestion pipeline responsible for writing the extracted
/// text and its embedding vector to a search index.
/// <para>
/// The default implementation is intentionally a no-op — swap it out for a
/// real provider (PostgreSQL <c>tsvector</c>, Azure AI Search, Elasticsearch, etc.)
/// by implementing this class or replacing it with a custom stage registered in DI.
/// </para>
/// </summary>
public class IndexStage
{
  /// <summary>
  /// Indexes the supplied text and vector.
  /// Override or replace this implementation to persist to a real search index.
  /// </summary>
  /// <param name="text">Extracted plain text of the document.</param>
  /// <param name="vector">Embedding vector produced by <see cref="EmbeddingStage"/>.</param>
  /// <param name="cancellationToken">Cancellation token.</param>
  public virtual Task IndexAsync(string text, float[] vector, CancellationToken cancellationToken = default)
    => Task.CompletedTask;
}
