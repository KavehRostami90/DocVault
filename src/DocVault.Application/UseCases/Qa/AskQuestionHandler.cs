using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using DocVault.Application.Abstractions.Cqrs;
using DocVault.Application.Abstractions.Qa;
using DocVault.Application.Common.Results;
using DocVault.Application.UseCases.Search;
using Microsoft.Extensions.Logging;

namespace DocVault.Application.UseCases.Qa;

/// <summary>
/// Lightweight RAG orchestration:
/// 1) retrieve top document chunks via the search pipeline (which already runs pgvector),
/// 2) use the pre-retrieved <see cref="SearchResultItem.MatchedChunkText"/> as context — no re-chunking,
/// 3) call QA generator,
/// 4) return answer + citations.
/// </summary>
public sealed partial class AskQuestionHandler : IQueryHandler<AskQuestionQuery, Result<AskQuestionResult>>
{
  private readonly SearchDocumentsHandler _search;
  private readonly IQuestionAnsweringService _qa;
  private readonly ILogger<AskQuestionHandler> _logger;

  public AskQuestionHandler(
    SearchDocumentsHandler search,
    IQuestionAnsweringService qa,
    ILogger<AskQuestionHandler> logger)
  {
    _search = search;
    _qa = qa;
    _logger = logger;
  }

  public async Task<Result<AskQuestionResult>> HandleAsync(AskQuestionQuery query, CancellationToken cancellationToken = default)
  {
    var search = await _search.HandleAsync(
      new SearchDocumentsQuery(query.Question, 1, Math.Max(1, query.MaxDocuments), query.OwnerId, query.IsAdmin),
      cancellationToken);

    if (!search.IsSuccess || search.Value is null)
      return Result<AskQuestionResult>.Failure(search.Error ?? "Failed to retrieve context documents.");

    var candidates = query.DocumentId.HasValue
      ? search.Value.Page.Items.Where(i => i.Document.Id.Value == query.DocumentId.Value).ToList()
      : search.Value.Page.Items.ToList();

    var contexts = BuildContexts(candidates, query.Question, query.MaxContexts);
    if (contexts.Count == 0)
    {
      return Result<AskQuestionResult>.Success(new AskQuestionResult(
        "I couldn't find relevant indexed text for that question.",
        [],
        AnsweredByModel: false));
    }

    QaAnswerResult answer;
    try
    {
      answer = await _qa.AnswerAsync(query.Question, contexts, cancellationToken);
    }
    catch (Exception ex)
    {
      LogQaServiceError(_logger, ex);
      return Result<AskQuestionResult>.Failure("QA service unavailable. Please try again later.");
    }

    var citations = contexts
      .Select(c => new AskQuestionCitation(c.DocumentId, c.DocumentTitle, c.Text[..Math.Min(180, c.Text.Length)], c.RetrievalScore))
      .ToList();

    return Result<AskQuestionResult>.Success(new AskQuestionResult(answer.Answer, citations, answer.AnsweredByModel));
  }

  /// <summary>
  /// Streaming variant: runs retrieval then yields LLM token deltas as an async sequence.
  /// The caller is responsible for writing SSE framing.
  /// </summary>
  public async IAsyncEnumerable<string> HandleStreamAsync(
    AskQuestionQuery query,
    [EnumeratorCancellation] CancellationToken cancellationToken = default)
  {
    var search = await _search.HandleAsync(
      new SearchDocumentsQuery(query.Question, 1, Math.Max(1, query.MaxDocuments), query.OwnerId, query.IsAdmin),
      cancellationToken);

    if (!search.IsSuccess || search.Value is null)
    {
      yield return "[ERROR] Failed to retrieve context documents.";
      yield break;
    }

    var candidates = query.DocumentId.HasValue
      ? search.Value.Page.Items.Where(i => i.Document.Id.Value == query.DocumentId.Value).ToList()
      : search.Value.Page.Items.ToList();

    var contexts = BuildContexts(candidates, query.Question, query.MaxContexts);
    if (contexts.Count == 0)
    {
      yield return "I couldn't find relevant indexed text for that question.";
      yield break;
    }

    await foreach (var token in _qa.AnswerStreamAsync(query.Question, contexts, cancellationToken))
      yield return token;
  }

  private static List<QaContextChunk> BuildContexts(IReadOnlyList<SearchResultItem> items, string question, int maxContexts)
  {
    var terms = Regex.Matches(question, @"\p{L}+|\p{N}+")
      .Select(m => m.Value.ToLowerInvariant())
      .Where(t => t.Length > 2)
      .Distinct()
      .ToArray();

    var chunks = new List<QaContextChunk>();
    var rank   = 0;

    foreach (var item in items)
    {
      // Use the chunk text already retrieved by the vector/FTS search — no re-chunking needed.
      var chunkText = item.MatchedChunkText;
      if (string.IsNullOrWhiteSpace(chunkText))
        continue;

      var lower       = chunkText.ToLowerInvariant();
      var lexicalHits = terms.Length == 0 ? 0 : terms.Count(t => lower.Contains(t, StringComparison.Ordinal));
      var lexicalScore = terms.Length == 0 ? 0d : (double)lexicalHits / terms.Length;
      var score       = Math.Round(item.Score * 0.7 + lexicalScore * 0.3, 4);
      chunks.Add(new QaContextChunk(item.Document.Id.Value, item.Document.Title, chunkText.Trim(), score, rank++));
    }

    return chunks
      .Where(c => !string.IsNullOrWhiteSpace(c.Text))
      .OrderByDescending(c => c.RetrievalScore)
      .Take(Math.Max(1, maxContexts))
      .ToList();
  }

  [LoggerMessage(Level = LogLevel.Error,
    Message = "QA service threw an unexpected exception.")]
  static partial void LogQaServiceError(ILogger logger, Exception ex);
}
