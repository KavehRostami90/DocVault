namespace DocVault.Infrastructure.Qa;

/// <summary>
/// Configuration for QA completion model.
/// </summary>
public sealed class QaOptions
{
  public const string Section = "QA";

  /// <summary>
  /// Chat-completions model name.
  /// </summary>
  public string Model { get; init; } = "gpt-4o-mini";

  /// <summary>
  /// Maximum tokens reserved for the final answer.
  /// </summary>
  public int MaxTokens { get; init; } = 250;
}
