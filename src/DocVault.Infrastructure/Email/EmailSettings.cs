namespace DocVault.Infrastructure.Email;

public sealed class EmailSettings
{
  public const string Section = "Email";

  public string ConnectionString { get; init; } = string.Empty;

  // e.g. DoNotReply@<resource>.germany.azurecomm.net (or a verified custom domain)
  public string SenderAddress { get; init; } = string.Empty;

  public string SenderDisplayName { get; init; } = "DocVault";

  public bool IsConfigured =>
    !string.IsNullOrWhiteSpace(ConnectionString) &&
    !string.IsNullOrWhiteSpace(SenderAddress);
}
