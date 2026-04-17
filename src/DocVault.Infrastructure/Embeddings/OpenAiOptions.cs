using System;

namespace DocVault.Infrastructure.Embeddings;

/// <summary>
/// Configuration options for the OpenAI embedding provider, including API key, model selection, and endpoint settings.
/// </summary>
public sealed class OpenAiOptions
{
  public const string Section = "OpenAI";

  /// <summary>
  /// Gets the API key used for authenticating requests to external services.
  /// </summary>
  public string ApiKey         { get; init; } = string.Empty;

  /// <summary>
  /// Gets the name of the embedding model to use when generating vector representations of text. The default value is "text-embedding-3-small", which is a compact model suitable for many embedding tasks.
  /// Depending on the OpenAI API version and available models, this can be changed to use different embedding models that may offer varying performance and accuracy characteristics.
  /// </summary>
  public string Model          { get; init; } = "text-embedding-3-small";

  /// <summary>
  /// Gets the base URL used to access the OpenAI API endpoints.
  /// </summary>
  public string BaseUrl        { get; init; } = "https://api.openai.com/v1";

  /// <summary>
  /// Gets the number of dimensions used for vector embeddings.
  /// </summary>
  /// <remarks>The value determines the size of the embedding vector generated or expected by the provider. The
  /// default is 1536, which matches common OpenAI embedding models.</remarks>
  /// <summary>
  /// Number of dimensions to request from the API. Set to 0 (default) to use the model's
  /// native output size and omit the parameter — required for local providers like Ollama
  /// that do not support dimension truncation. Set to a positive value only for OpenAI
  /// models that support it (e.g. text-embedding-3-*).
  /// </summary>
  public int Dimensions { get; init; } = 0;

  /// <summary>
  /// Gets a value indicating whether an OpenAI-compatible endpoint is configured.
  /// Supports either:
  /// <list type="bullet">
  ///   <item><description>Hosted OpenAI/Azure OpenAI with API key.</description></item>
  ///   <item><description>Local OpenAI-compatible endpoints (for example Ollama) via a non-default BaseUrl.</description></item>
  /// </list>
  /// </summary>
  public bool IsConfigured =>
    !string.IsNullOrWhiteSpace(ApiKey) ||
    HasConfiguredCustomBaseUrl();

  private bool HasConfiguredCustomBaseUrl()
  {
    if (string.IsNullOrWhiteSpace(BaseUrl))
    {
      return false;
    }

    if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out var baseUri))
    {
      return false;
    }

    const string defaultBaseUrl = "https://api.openai.com/v1";

    return !string.Equals(
      baseUri.ToString().TrimEnd('/'),
      defaultBaseUrl,
      StringComparison.OrdinalIgnoreCase);
  }
}
