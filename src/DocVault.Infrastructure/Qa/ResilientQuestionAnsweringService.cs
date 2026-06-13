using System.Runtime.CompilerServices;
using DocVault.Application.Abstractions.Qa;
using Microsoft.Extensions.Logging;

namespace DocVault.Infrastructure.Qa;

/// <summary>
/// Decorator that catches failures from the primary QA service and falls back to
/// <see cref="FallbackQuestionAnsweringService"/> so callers always receive a degraded
/// but useful response rather than a 5xx when the LLM endpoint is unavailable.
/// </summary>
public sealed partial class ResilientQuestionAnsweringService : IQuestionAnsweringService
{
  private readonly IQuestionAnsweringService _inner;
  private readonly IQuestionAnsweringService _fallback;
  private readonly ILogger<ResilientQuestionAnsweringService> _logger;

  public ResilientQuestionAnsweringService(
    IQuestionAnsweringService inner,
    IQuestionAnsweringService fallback,
    ILogger<ResilientQuestionAnsweringService> logger)
  {
    _inner    = inner;
    _fallback = fallback;
    _logger   = logger;
  }

  public async Task<QaAnswerResult> AnswerAsync(
    string question,
    IReadOnlyList<QaContextChunk> contexts,
    CancellationToken cancellationToken = default)
  {
    try
    {
      return await _inner.AnswerAsync(question, contexts, cancellationToken);
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
      LogQaUnavailable(_logger, ex);
      return await _fallback.AnswerAsync(question, contexts, cancellationToken);
    }
  }

  public async IAsyncEnumerable<string> AnswerStreamAsync(
    string question,
    IReadOnlyList<QaContextChunk> contexts,
    [EnumeratorCancellation] CancellationToken cancellationToken = default)
  {
    // Probe the inner stream before yielding anything. All real failure modes —
    // connection refused, 4xx/5xx, timeout — surface before the first token because
    // OpenAiQuestionAnsweringService calls EnsureSuccessStatusCode before the yield
    // loop. If the first MoveNextAsync throws, we switch cleanly to the fallback.
    // Mid-stream failures (network drop after headers are received) end the stream
    // without fallback: mixing partial LLM output with a fallback excerpt would
    // produce a worse result than stopping cleanly.
    string? firstToken  = null;
    bool    innerFailed = false;

    var enumerator = _inner
      .AnswerStreamAsync(question, contexts, cancellationToken)
      .GetAsyncEnumerator(cancellationToken);

    try
    {
      if (await enumerator.MoveNextAsync())
        firstToken = enumerator.Current;
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
      LogQaUnavailable(_logger, ex);
      innerFailed = true;
    }

    if (innerFailed)
    {
      await enumerator.DisposeAsync();
      await foreach (var t in _fallback.AnswerStreamAsync(question, contexts, cancellationToken))
        yield return t;
      yield break;
    }

    if (firstToken is not null)
      yield return firstToken;

    // yield return is allowed in try-finally (no catch) — streams the remainder and
    // disposes the enumerator regardless of whether the consumer cancels mid-stream.
    try
    {
      while (await enumerator.MoveNextAsync())
        yield return enumerator.Current;
    }
    finally
    {
      await enumerator.DisposeAsync();
    }
  }

  [LoggerMessage(Level = LogLevel.Warning,
    Message = "QA model unavailable; falling back to excerpt-based answer.")]
  private static partial void LogQaUnavailable(ILogger logger, Exception ex);
}
