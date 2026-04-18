namespace DocVault.Infrastructure.Auth;

public sealed class AuthSettings
{
  public const string Section = "Auth";

  public string JwtSigningKey { get; init; } = string.Empty;
  public string JwtIssuer { get; init; } = "docvault";
  public string JwtAudience { get; init; } = "docvault-ui";
  public int AccessTokenExpiryMinutes { get; init; } = 15;
  public int RefreshTokenExpiryDays { get; init; } = 7;
  public string AdminEmail { get; init; } = string.Empty;
  public string AdminPassword { get; init; } = string.Empty;
  public string FrontendBaseUrl { get; init; } = "http://localhost:5173";

  public bool IsConfigured => !string.IsNullOrWhiteSpace(JwtSigningKey);
}
