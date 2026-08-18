using DocVault.Application.Abstractions.Email;
using Microsoft.Extensions.Logging;

namespace DocVault.Infrastructure.Email;

// ACS not configured — do not log raw links/tokens to avoid storing sensitive data.
internal sealed class LogEmailService(ILogger<LogEmailService> logger) : IEmailService
{
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

    var local = email[..atIndex];
    var domain = email[(atIndex + 1)..];

    var maskedLocal = local.Length <= 2
      ? new string('*', local.Length)
      : $"{local[0]}***{local[^1]}";

    var maskedDomain = domain.Length <= 2
      ? new string('*', domain.Length)
      : $"{domain[0]}***{domain[^1]}";

    return $"{maskedLocal}@{maskedDomain}";
  }

  public Task SendPasswordResetAsync(string toEmail, string resetLink, CancellationToken ct = default)
  {
    var safeEmail = MaskEmailForLogs(toEmail);
    logger.LogWarning(
      "PASSWORD RESET requested for {Email}. Reset link redacted; configure a real email provider to deliver reset URLs securely.",
      safeEmail);
    return Task.CompletedTask;
  }

  public Task SendEmailConfirmationAsync(string toEmail, string confirmationLink, CancellationToken ct = default)
  {
    var safeEmail = MaskEmailForLogs(toEmail);
    logger.LogWarning(
      "EMAIL VERIFICATION requested for {Email}. Confirmation link redacted; configure a real email provider to deliver verification URLs securely.",
      safeEmail);
    return Task.CompletedTask;
  }
}
