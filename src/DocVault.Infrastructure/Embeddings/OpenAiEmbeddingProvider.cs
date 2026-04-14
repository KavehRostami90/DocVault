using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using DocVault.Application.Abstractions.Embeddings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DocVault.Infrastructure.Embeddings;

public sealed partial class OpenAiEmbeddingProvider : IEmbeddingProvider
{
  private readonly HttpClient _http;
  private readonly OpenAiOptions _options;
  private readonly ILogger<OpenAiEmbeddingProvider> _logger;

  public OpenAiEmbeddingProvider(
    HttpClient http,
    IOptions<OpenAiOptions> options,
    ILogger<OpenAiEmbeddingProvider> logger)
  {
    _http    = http;
    _options = options.Value;
    _logger  = logger;

    _http.BaseAddress = new Uri(_options.BaseUrl.TrimEnd('/') + "/");
    _http.DefaultRequestHeaders.Authorization =
      new AuthenticationHeaderValue("Bearer", _options.ApiKey);
  }

  public async Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default)
  {
    // The `dimensions` parameter is only supported by certain OpenAI models (text-embedding-3-*).
    // Local providers such as Ollama ignore or reject it, so we only include it when explicitly set.
    object body = _options.Dimensions > 0
      ? new { model = _options.Model, input = text, dimensions = _options.Dimensions }
      : new { model = _options.Model, input = text };

    var response = await _http.PostAsJsonAsync("embeddings", body, cancellationToken);
    response.EnsureSuccessStatusCode();

    var result = await response.Content.ReadFromJsonAsync<EmbeddingResponse>(cancellationToken)
      ?? throw new InvalidOperationException("OpenAI returned an empty embedding response.");

    if (result.Data is not [{ Embedding: { } vector }, ..])
      throw new InvalidOperationException("OpenAI returned no embedding data.");

    LogEmbedded(_logger, text.Length, _options.Model);
    return vector;
  }

  [LoggerMessage(Level = LogLevel.Debug,
    Message = "Embedded {CharCount} chars using model {Model}.")]
  private static partial void LogEmbedded(ILogger logger, int charCount, string model);

  // ----- response shapes -----

  private sealed record EmbeddingResponse(
    [property: JsonPropertyName("data")]   List<EmbeddingData> Data);

  private sealed record EmbeddingData(
    [property: JsonPropertyName("embedding")] float[] Embedding);
}
