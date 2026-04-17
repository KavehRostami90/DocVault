using DocVault.Application.Abstractions.Qa;

namespace DocVault.Infrastructure.Qa;

/// <summary>
/// Deterministic fallback QA when no LLM endpoint is configured.
/// </summary>
public sealed class FallbackQuestionAnsweringService : IQuestionAnsweringService
{
  public Task<QaAnswerResult> AnswerAsync(string question, IReadOnlyList<QaContextChunk> contexts, CancellationToken cancellationToken = default)
  {
    if (contexts.Count == 0)
      return Task.FromResult(new QaAnswerResult("I couldn't find relevant indexed text for that question.", AnsweredByModel: false));

    var best = contexts[0];
    var excerpt = best.Text[..Math.Min(220, best.Text.Length)];
    return Task.FromResult(new QaAnswerResult($"Possible answer from '{best.DocumentTitle}': {excerpt}", AnsweredByModel: false));
  }
}
