using DocVault.Application.Abstractions.Email;
using Microsoft.Extensions.Logging;

namespace DocVault.Infrastructure.Email;

// ACS not configured — logs links to console so a developer can relay them manually.
internal sealed class LogEmailService(ILogger<LogEmailService> logger) : IEmailService
{
  public Task SendPasswordResetAsync(string toEmail, string resetLink, CancellationToken ct = default)
  {
    var maskedEmail = MaskEmailForLogs(toEmail);
    logger.LogWarning(
      "PASSWORD RESET for {EmailMasked} — share this link: {ResetLink}",
      maskedEmail, resetLink);
    return Task.CompletedTask;
  }

  public Task SendEmailConfirmationAsync(string toEmail, string confirmationLink, CancellationToken ct = default)
  {
    var maskedEmail = MaskEmailForLogs(toEmail);
    logger.LogWarning(
      "EMAIL VERIFICATION for {EmailMasked} — share this link: {ConfirmationLink}",
      maskedEmail, confirmationLink);
    return Task.CompletedTask;
  }

  private static string MaskEmailForLogs(string email)
  {
    if (string.IsNullOrWhiteSpace(email))
    {
      return "[redacted]";
    }

    var atIndex = email.IndexOf('@');
    if (atIndex <= 0 || atIndex == email.Length - 1)
    {
      return "[redacted]";
    }

    var localPart = email[..atIndex];
    var domainPart = email[(atIndex + 1)..];

    var maskedLocal = localPart.Length == 1
      ? "*"
      : $"{localPart[0]}***";

    return $"{maskedLocal}@{domainPart}";
  }
}
