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
  public string Model { get; init; } = "llama3.1";

  /// <summary>
  /// Maximum tokens reserved for the final answer.
  /// </summary>
  public int MaxTokens { get; init; } = 250;

  /// <summary>
  /// Per-request deadline in seconds for LLM calls. Default: 120 s.
  /// Prevents slow models from blocking indefinitely. Set to 0 to disable.
  /// </summary>
  public int TimeoutSeconds { get; init; } = 120;
}
