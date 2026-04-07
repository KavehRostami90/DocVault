namespace DocVault.Infrastructure.Auth;

public sealed class RefreshToken
{
  public Guid Id { get; init; } = Guid.NewGuid();
  public string Token { get; init; } = string.Empty;
  public string UserId { get; init; } = string.Empty;
  public DateTimeOffset ExpiresAt { get; init; }
  public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
  public DateTimeOffset? RevokedAt { get; set; }

  public bool IsExpired => DateTimeOffset.UtcNow >= ExpiresAt;
  public bool IsActive => RevokedAt is null && !IsExpired;
}
