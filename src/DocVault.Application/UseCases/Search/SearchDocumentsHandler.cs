using DocVault.Application.Abstractions.Cqrs;
using DocVault.Application.Abstractions.Embeddings;
using DocVault.Application.Abstractions.Persistence;
using DocVault.Application.Common.Results;
using Microsoft.Extensions.Logging;

namespace DocVault.Application.UseCases.Search;

/// <summary>
/// Handles document search. Embeds the query with the configured provider for semantic
/// (pgvector cosine similarity) search. If embedding fails (e.g. Ollama not running),
/// the search falls back to PostgreSQL full-text search automatically.
/// Results are cached via <see cref="ISearchResultCache"/> to reduce
/// repeated embedding calls and database load for popular queries.
/// </summary>
public sealed partial class SearchDocumentsHandler : IQueryHandler<SearchDocumentsQuery, Result<SearchPageResult>>
{
  private readonly IDocumentRepository _documents;
  private readonly IEmbeddingProvider  _embedding;
  private readonly ISearchResultCache  _cache;
  private readonly ILogger<SearchDocumentsHandler> _logger;

  public SearchDocumentsHandler(
    IDocumentRepository documents,
    IEmbeddingProvider embedding,
    ISearchResultCache cache,
    ILogger<SearchDocumentsHandler> logger)
  {
    _documents = documents;
    _embedding = embedding;
    _cache     = cache;
    _logger    = logger;
  }

  public async Task<Result<SearchPageResult>> HandleAsync(SearchDocumentsQuery query, CancellationToken cancellationToken = default)
  {
    var ownerId = query.IsAdmin ? null : query.OwnerId;

    var cacheKey = BuildCacheKey(ownerId, query.Query, query.Page, query.Size);
    var cached   = await _cache.GetAsync(cacheKey, cancellationToken);
    if (cached is not null)
      return Result<SearchPageResult>.Success(cached);

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

    var hasTerms = query.Query.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length > 0;
    var mode = queryVector is null ? SearchMode.Keyword
             : hasTerms           ? SearchMode.Hybrid
             :                      SearchMode.Semantic;

    var result = new SearchPageResult(page, mode);
    await _cache.SetAsync(cacheKey, result, cancellationToken);

    return Result<SearchPageResult>.Success(result);
  }

  private static string BuildCacheKey(Guid? ownerId, string query, int page, int size)
  {
    var normalised = string.Join(' ', query
      .Trim()
      .ToLowerInvariant()
      .Split(' ', StringSplitOptions.RemoveEmptyEntries));

    return $"search:{ownerId?.ToString() ?? "admin"}:{normalised}:{page}:{size}";
  }

  [LoggerMessage(Level = LogLevel.Warning,
    Message = "Failed to embed search query; falling back to full-text search.")]
  static partial void LogEmbeddingFailed(ILogger logger, Exception ex);
}
