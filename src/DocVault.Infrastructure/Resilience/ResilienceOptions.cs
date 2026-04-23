namespace DocVault.Infrastructure.Resilience;

public sealed class ResilienceOptions
{
  public const string Section = "Resilience";

  public ClientResilienceOptions Embedding { get; init; } = new();
  public ClientResilienceOptions Qa        { get; init; } = new() { AttemptTimeoutSeconds = 90, TotalTimeoutSeconds = 300, MaxRetryAttempts = 2 };
}

public sealed class ClientResilienceOptions
{
  public int AttemptTimeoutSeconds { get; init; } = 15;
  public int TotalTimeoutSeconds   { get; init; } = 60;
  public int MaxRetryAttempts      { get; init; } = 3;
}
