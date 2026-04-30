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
    if (!string.IsNullOrWhiteSpace(_options.ApiKey))
      _http.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue("Bearer", _options.ApiKey);

    LogProviderReady(
      _logger,
      _options.BaseUrl,
      _options.Model,
      _options.Dimensions > 0 ? _options.Dimensions.ToString() : "native");
  }

  public async Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default)
  {
    var result = await EmbedBatchAsync([text], cancellationToken);
    return result[0];
  }

  public async Task<IReadOnlyList<float[]>> EmbedBatchAsync(
    IReadOnlyList<string> texts,
    CancellationToken cancellationToken = default)
  {
    // The `dimensions` parameter is only supported by certain OpenAI models (text-embedding-3-*).
    // Local providers such as Ollama ignore or reject it, so we only include it when explicitly set.
    object body = _options.Dimensions > 0
      ? new { model = _options.Model, input = texts, dimensions = _options.Dimensions }
      : new { model = _options.Model, input = texts };

    LogBatchRequestSent(_logger, _options.Model, texts.Count);

    var response = await _http.PostAsJsonAsync("embeddings", body, cancellationToken);

    if (!response.IsSuccessStatusCode)
    {
      var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
      LogRequestFailed(_logger, (int)response.StatusCode, $"{_options.BaseUrl} ({_options.Model})", errorBody);
      response.EnsureSuccessStatusCode();
    }

    var result = await response.Content.ReadFromJsonAsync<EmbeddingResponse>(cancellationToken)
      ?? throw new InvalidOperationException("Embedding API returned an empty response body.");

    if (result.Data is not { Count: > 0 })
    {
      LogEmptyEmbedding(_logger, _options.Model);
      throw new InvalidOperationException("Embedding API returned no embedding data.");
    }

    // The API may return items out of order — sort by index before returning.
    var vectors = result.Data
      .OrderBy(d => d.Index)
      .Select(d => d.Embedding)
      .ToArray();

    LogBatchEmbedded(_logger, texts.Count, _options.Model, vectors[0].Length);
    return vectors;
  }

  [LoggerMessage(Level = LogLevel.Information,
    Message = "Embedding provider ready — baseUrl={BaseUrl}, model={Model}, dimensions={Dimensions}.")]
  private static partial void LogProviderReady(ILogger logger, string baseUrl, string model, string dimensions);

  [LoggerMessage(Level = LogLevel.Debug,
    Message = "Sending batch embed request — model={Model}, count={Count} texts.")]
  private static partial void LogBatchRequestSent(ILogger logger, string model, int count);

  [LoggerMessage(Level = LogLevel.Debug,
    Message = "Batch embedded {Count} texts using model {Model} — returned {Dimensions} dimensions each.")]
  private static partial void LogBatchEmbedded(ILogger logger, int count, string model, int dimensions);
  
  [LoggerMessage(EventId = 1, Level = LogLevel.Error,
    Message = "Embedding API request failed — HTTP {StatusCode}, endpoint={Endpoint}. Response body: {Body}")]
  private static partial void LogRequestFailed(ILogger logger, int statusCode, string endpoint, string body);

  [LoggerMessage(EventId = 2, Level = LogLevel.Error,
    Message = "Embedding API returned no data for model {Model}.")]
  private static partial void LogEmptyEmbedding(ILogger logger, string model);

  // ----- response shapes -----

  private sealed record EmbeddingResponse(
    [property: JsonPropertyName("data")] List<EmbeddingData> Data);

  private sealed record EmbeddingData(
    [property: JsonPropertyName("embedding")] float[] Embedding,
    [property: JsonPropertyName("index")]     int     Index);
}
