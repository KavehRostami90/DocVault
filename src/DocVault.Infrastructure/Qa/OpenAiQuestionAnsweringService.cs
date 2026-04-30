using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DocVault.Application.Abstractions.Qa;
using DocVault.Infrastructure.Embeddings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DocVault.Infrastructure.Qa;

public sealed partial class OpenAiQuestionAnsweringService : IQuestionAnsweringService
{
  private readonly HttpClient _http;
  private readonly OpenAiOptions _openAiOptions;
  private readonly QaOptions _qaOptions;
  private readonly ILogger<OpenAiQuestionAnsweringService> _logger;

  public OpenAiQuestionAnsweringService(
    HttpClient http,
    IOptions<OpenAiOptions> openAiOptions,
    IOptions<QaOptions> qaOptions,
    ILogger<OpenAiQuestionAnsweringService> logger)
  {
    _http = http;
    _openAiOptions = openAiOptions.Value;
    _qaOptions = qaOptions.Value;
    _logger = logger;

    _http.BaseAddress = new Uri(_openAiOptions.BaseUrl.TrimEnd('/') + "/");

    if (!string.IsNullOrWhiteSpace(_openAiOptions.ApiKey))
    {
      _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _openAiOptions.ApiKey);
    }
  }

  public async Task<QaAnswerResult> AnswerAsync(string question, IReadOnlyList<QaContextChunk> contexts, CancellationToken cancellationToken = default)
  {
    if (contexts.Count == 0)
      return new QaAnswerResult("I couldn't find relevant indexed text for that question.", AnsweredByModel: false);

    using var cts = BuildTimeoutCts(cancellationToken);

    var contextText = BuildContext(contexts);

    var body = new ChatRequest(
      _qaOptions.Model,
      BuildMessages(question, contextText),
      _qaOptions.MaxTokens,
      Stream: false);

    var response = await _http.PostAsJsonAsync("chat/completions", body, cts.Token);
    if (!response.IsSuccessStatusCode)
    {
      var err = await response.Content.ReadAsStringAsync(cts.Token);
      LogChatFailed(_logger, (int)response.StatusCode, err);
      response.EnsureSuccessStatusCode();
    }

    var payload = await response.Content.ReadFromJsonAsync<ChatResponse>(cts.Token);
    var answer = payload?.Choices?.FirstOrDefault()?.Message?.Content?.Trim();
    return string.IsNullOrWhiteSpace(answer)
      ? new QaAnswerResult("I don't know from the indexed documents.", AnsweredByModel: true)
      : new QaAnswerResult(answer, AnsweredByModel: true);
  }

  public async IAsyncEnumerable<string> AnswerStreamAsync(
    string question,
    IReadOnlyList<QaContextChunk> contexts,
    [EnumeratorCancellation] CancellationToken cancellationToken = default)
  {
    if (contexts.Count == 0)
    {
      yield return "I couldn't find relevant indexed text for that question.";
      yield break;
    }

    using var cts = BuildTimeoutCts(cancellationToken);

    var contextText = BuildContext(contexts);
    var body        = new ChatRequest(
      _qaOptions.Model,
      BuildMessages(question, contextText),
      _qaOptions.MaxTokens,
      Stream: true);

    using var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
    {
      Content = JsonContent.Create(body),
    };
    request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

    using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);
    if (!response.IsSuccessStatusCode)
    {
      var err = await response.Content.ReadAsStringAsync(cts.Token);
      LogChatFailed(_logger, (int)response.StatusCode, err);
      response.EnsureSuccessStatusCode();
    }

    await using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
    using var reader        = new StreamReader(stream);

    while (!reader.EndOfStream && !cts.Token.IsCancellationRequested)
    {
      var line = await reader.ReadLineAsync(cts.Token);
      if (string.IsNullOrEmpty(line) || !line.StartsWith("data: ", StringComparison.Ordinal))
        continue;

      var data = line["data: ".Length..];
      if (data == "[DONE]")
        yield break;

      StreamDelta? delta = null;
      try { delta = JsonSerializer.Deserialize<StreamDelta>(data); }
      catch (JsonException) { /* malformed chunk — skip */ }

      var token = delta?.Choices?.FirstOrDefault()?.Delta?.Content;
      if (!string.IsNullOrEmpty(token))
        yield return token;
    }
  }

  // -------------------------------------------------------------------------
  // Helpers
  // -------------------------------------------------------------------------

  private CancellationTokenSource BuildTimeoutCts(CancellationToken linked)
  {
    var cts = CancellationTokenSource.CreateLinkedTokenSource(linked);
    if (_qaOptions.TimeoutSeconds > 0)
      cts.CancelAfter(TimeSpan.FromSeconds(_qaOptions.TimeoutSeconds));
    return cts;
  }

  private static List<ChatMessage> BuildMessages(string question, string contextText) =>
  [
    new ChatMessage("system",
      "You answer strictly from the provided context. " +
      "Always respond in the same language as the user's question — never switch to a different language. " +
      "If the context does not contain enough information, say so briefly in the question's language."),
    new ChatMessage("user",
      $"Question: {question}\n\nContext:\n{contextText}\n\nAnswer the question using only the context above. Respond in the same language as the question.")
  ];

  private static string BuildContext(IReadOnlyList<QaContextChunk> contexts)
  {
    var sb = new StringBuilder();
    for (var i = 0; i < contexts.Count; i++)
    {
      var c = contexts[i];
      sb.AppendLine($"[{i + 1}] Doc={c.DocumentTitle} (id={c.DocumentId})");
      sb.AppendLine(c.Text);
      sb.AppendLine();
    }

    return sb.ToString();
  }

  [LoggerMessage(EventId = 1, Level = LogLevel.Error,
    Message = "QA chat request failed. HTTP {StatusCode}. Body: {Body}")]
  private static partial void LogChatFailed(ILogger logger, int statusCode, string body);

  // -------------------------------------------------------------------------
  // Wire types
  // -------------------------------------------------------------------------

  private sealed record ChatRequest(
    [property: JsonPropertyName("model")]      string Model,
    [property: JsonPropertyName("messages")]   List<ChatMessage> Messages,
    [property: JsonPropertyName("max_tokens")] int MaxTokens,
    [property: JsonPropertyName("stream")]     bool Stream);

  private sealed record ChatMessage(
    [property: JsonPropertyName("role")]    string Role,
    [property: JsonPropertyName("content")] string Content);

  private sealed record ChatResponse(
    [property: JsonPropertyName("choices")] List<Choice>? Choices);

  private sealed record Choice(
    [property: JsonPropertyName("message")] ChatMessage? Message);

  // SSE streaming wire types
  private sealed record StreamDelta(
    [property: JsonPropertyName("choices")] List<StreamChoice>? Choices);

  private sealed record StreamChoice(
    [property: JsonPropertyName("delta")] DeltaContent? Delta);

  private sealed record DeltaContent(
    [property: JsonPropertyName("content")] string? Content);
}
