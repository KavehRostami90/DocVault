using System.Text.RegularExpressions;
using DocVault.Application.Abstractions.Qa;
using DocVault.Application.Common.Results;
using DocVault.Application.UseCases.Search;

namespace DocVault.Application.UseCases.Qa;

/// <summary>
/// Lightweight RAG orchestration:
/// 1) retrieve top documents,
/// 2) split to context chunks,
/// 3) call QA generator,
/// 4) return answer + citations.
/// </summary>
public sealed class AskQuestionHandler
{
  private readonly SearchDocumentsHandler _search;
  private readonly IQuestionAnsweringService _qa;

  public AskQuestionHandler(SearchDocumentsHandler search, IQuestionAnsweringService qa)
  {
    _search = search;
    _qa = qa;
  }

  public async Task<Result<AskQuestionResult>> HandleAsync(AskQuestionQuery query, CancellationToken cancellationToken = default)
  {
    var search = await _search.HandleAsync(
      new SearchDocumentsQuery(query.Question, 1, Math.Max(1, query.MaxDocuments), query.OwnerId, query.IsAdmin),
      cancellationToken);

    if (!search.IsSuccess || search.Value is null)
      return Result<AskQuestionResult>.Failure(search.Error ?? "Failed to retrieve context documents.");

    var candidates = query.DocumentId.HasValue
      ? search.Value.Items.Where(i => i.Document.Id.Value == query.DocumentId.Value).ToList()
      : search.Value.Items;

    var contexts = BuildContexts(candidates, query.Question, query.MaxContexts);
    if (contexts.Count == 0)
    {
      return Result<AskQuestionResult>.Success(new AskQuestionResult(
        "I couldn't find relevant indexed text for that question.",
        [],
        AnsweredByModel: false));
    }

    var answer = await _qa.AnswerAsync(query.Question, contexts, cancellationToken);

    var citations = contexts
      .Select(c => new AskQuestionCitation(c.DocumentId, c.DocumentTitle, c.Text[..Math.Min(180, c.Text.Length)], c.RetrievalScore))
      .ToList();

    return Result<AskQuestionResult>.Success(new AskQuestionResult(answer.Answer, citations, answer.AnsweredByModel));
  }

  private static List<QaContextChunk> BuildContexts(IReadOnlyList<SearchResultItem> items, string question, int maxContexts)
  {
    var terms = Regex.Matches(question.ToLowerInvariant(), "[a-z0-9]+")
      .Select(m => m.Value)
      .Where(t => t.Length > 2)
      .Distinct()
      .ToArray();

    var chunks = new List<QaContextChunk>();

    foreach (var item in items)
    {
      var doc = item.Document;
      var text = doc.Text ?? string.Empty;
      if (string.IsNullOrWhiteSpace(text))
        continue;

      foreach (var (chunkText, idx) in Chunk(text, 700, 120).Select((c, i) => (c, i)))
      {
        var lower = chunkText.ToLowerInvariant();
        var lexicalHits = terms.Count(t => lower.Contains(t, StringComparison.Ordinal));
        var lexicalScore = terms.Length == 0 ? 0d : (double)lexicalHits / terms.Length;
        var score = Math.Round(item.Score * 0.7 + lexicalScore * 0.3, 4);
        chunks.Add(new QaContextChunk(doc.Id.Value, doc.Title, chunkText.Trim(), score, idx));
      }
    }

    return chunks
      .Where(c => !string.IsNullOrWhiteSpace(c.Text))
      .OrderByDescending(c => c.RetrievalScore)
      .Take(Math.Max(1, maxContexts))
      .ToList();
  }

  private static IEnumerable<string> Chunk(string text, int window, int overlap)
  {
    if (string.IsNullOrWhiteSpace(text))
      yield break;

    var clean = Regex.Replace(text, "\\s+", " ").Trim();
    if (clean.Length <= window)
    {
      yield return clean;
      yield break;
    }

    var step = Math.Max(1, window - overlap);
    for (var i = 0; i < clean.Length; i += step)
    {
      var len = Math.Min(window, clean.Length - i);
      if (len <= 0) break;
      yield return clean.Substring(i, len);
      if (i + len >= clean.Length) break;
    }
  }

  internal static string FallbackAnswer(QaContextChunk best)
    => $"Possible answer from '{best.DocumentTitle}': {best.Text[..Math.Min(220, best.Text.Length)]}";
}
