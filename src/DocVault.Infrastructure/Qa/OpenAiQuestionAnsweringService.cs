using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
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

    var contextText = BuildContext(contexts);

    var body = new ChatRequest(
      _qaOptions.Model,
      [
        new ChatMessage("system",
          "You answer strictly from the provided context. " +
          "Always respond in the same language as the user's question — never switch to a different language. " +
          "If the context does not contain enough information, say so briefly in the question's language."),
        new ChatMessage("user", $"Question: {question}\n\nContext:\n{contextText}\n\nAnswer the question using only the context above. Respond in the same language as the question.")
      ],
      _qaOptions.MaxTokens);

    var response = await _http.PostAsJsonAsync("chat/completions", body, cancellationToken);
    if (!response.IsSuccessStatusCode)
    {
      var err = await response.Content.ReadAsStringAsync(cancellationToken);
      LogChatFailed(_logger, (int)response.StatusCode, err);
      response.EnsureSuccessStatusCode();
    }

    var payload = await response.Content.ReadFromJsonAsync<ChatResponse>(cancellationToken);
    var answer = payload?.Choices?.FirstOrDefault()?.Message?.Content?.Trim();
    return string.IsNullOrWhiteSpace(answer)
      ? new QaAnswerResult("I don't know from the indexed documents.", AnsweredByModel: true)
      : new QaAnswerResult(answer, AnsweredByModel: true);
  }

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

  private sealed record ChatRequest(
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("messages")] List<ChatMessage> Messages,
    [property: JsonPropertyName("max_tokens")] int MaxTokens);

  private sealed record ChatMessage(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("content")] string Content);

  private sealed record ChatResponse(
    [property: JsonPropertyName("choices")] List<Choice>? Choices);

  private sealed record Choice(
    [property: JsonPropertyName("message")] ChatMessage? Message);
}
