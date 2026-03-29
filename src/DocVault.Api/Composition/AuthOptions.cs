namespace DocVault.Api.Composition;

public sealed class AuthOptions
{
  public const string Section = "Auth";

  public string Authority { get; init; } = string.Empty;
  public string Audience  { get; init; } = string.Empty;

  public bool IsConfigured =>
    !string.IsNullOrWhiteSpace(Authority) && !string.IsNullOrWhiteSpace(Audience);
}
