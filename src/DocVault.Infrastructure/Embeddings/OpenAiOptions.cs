namespace DocVault.Infrastructure.Embeddings;

public sealed class OpenAiOptions
{
  public const string Section = "OpenAI";

  public string ApiKey         { get; init; } = string.Empty;
  public string Model          { get; init; } = "text-embedding-3-small";
  public string BaseUrl        { get; init; } = "https://api.openai.com/v1";
  public int    Dimensions     { get; init; } = 1536;

  public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
}
