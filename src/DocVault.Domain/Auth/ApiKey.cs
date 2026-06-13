namespace DocVault.Domain.Auth;

public sealed class ApiKey
{
  public Guid Id { get; init; } = Guid.NewGuid();
  public string UserId { get; init; } = string.Empty;
  public string Name { get; init; } = string.Empty;

  /// <summary>SHA-256 hex digest of the raw key — never stored in plaintext.</summary>
  public string KeyHash { get; init; } = string.Empty;

  /// <summary>First 12 characters of the raw key for display purposes (e.g. "dvk_xXxXxXxX").</summary>
  public string KeyPrefix { get; init; } = string.Empty;

  public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
  public DateTimeOffset? LastUsedAt { get; set; }
  public DateTimeOffset? ExpiresAt { get; init; }
  public bool IsRevoked { get; set; }

  public bool IsActive => !IsRevoked && (ExpiresAt is null || ExpiresAt > DateTimeOffset.UtcNow);

  public void Revoke() => IsRevoked = true;
  public void RecordUse() => LastUsedAt = DateTimeOffset.UtcNow;
}
