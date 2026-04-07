namespace DocVault.Api.Composition;

/// <summary>
/// Typed options for the "Auth" configuration section.
/// Bind from appsettings.json / environment variables.
/// </summary>
public sealed class AuthOptions
{
  public const string Section = "Auth";

  public string JwtSigningKey { get; init; } = string.Empty;
  public string JwtIssuer { get; init; } = "docvault";
  public string JwtAudience { get; init; } = "docvault-ui";
  public int AccessTokenExpiryMinutes { get; init; } = 15;
  public int RefreshTokenExpiryDays { get; init; } = 7;

  public bool IsConfigured => !string.IsNullOrWhiteSpace(JwtSigningKey);
}
