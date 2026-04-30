namespace DocVault.Infrastructure.Persistence;

public sealed class DatabaseOptions
{
  public const string Section = "Database";

  /// <summary>
  /// PostgreSQL command timeout in seconds.
  /// Increase this when bulk chunk inserts (large documents with many 768-dim embeddings) timeout.
  /// Default: 120 seconds.
  /// </summary>
  public int CommandTimeoutSeconds { get; init; } = 120;
}
