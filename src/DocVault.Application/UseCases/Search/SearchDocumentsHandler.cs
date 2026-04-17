using DocVault.Application.Abstractions.Embeddings;
using DocVault.Application.Abstractions.Persistence;
using DocVault.Application.Common.Paging;
using DocVault.Application.Common.Results;
using Microsoft.Extensions.Logging;

namespace DocVault.Application.UseCases.Search;

/// <summary>
/// Handles document search. Embeds the query with the configured provider for semantic
/// (pgvector cosine similarity) search. If embedding fails (e.g. Ollama not running),
/// the search falls back to PostgreSQL full-text search automatically.
/// </summary>
public sealed partial class SearchDocumentsHandler
{
  private readonly IDocumentRepository _documents;
  private readonly IEmbeddingProvider  _embedding;
  private readonly ILogger<SearchDocumentsHandler> _logger;

  public SearchDocumentsHandler(
    IDocumentRepository documents,
    IEmbeddingProvider embedding,
    ILogger<SearchDocumentsHandler> logger)
  {
    _documents = documents;
    _embedding = embedding;
    _logger    = logger;
  }

  public async Task<Result<SearchPageResult>> HandleAsync(SearchDocumentsQuery query, CancellationToken cancellationToken = default)
  {
    var ownerId = query.IsAdmin ? null : query.OwnerId;

    // Try to embed the query for semantic search; fall back to keyword search on failure.
    float[]? queryVector = null;
    try
    {
      queryVector = await _embedding.EmbedAsync(query.Query, cancellationToken);
    }
    catch (Exception ex)
    {
      LogEmbeddingFailed(_logger, ex);
    }

    var page = await _documents.SearchAsync(query.Query, query.Page, query.Size, ownerId, queryVector, cancellationToken);
    return Result<SearchPageResult>.Success(new SearchPageResult(page, UsedSemanticSearch: queryVector is not null));
  }

  [LoggerMessage(Level = LogLevel.Warning,
    Message = "Failed to embed search query; falling back to full-text search.")]
  static partial void LogEmbeddingFailed(ILogger logger, Exception ex);
}
