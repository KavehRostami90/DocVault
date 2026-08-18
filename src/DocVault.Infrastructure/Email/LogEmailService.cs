using DocVault.Application.Abstractions.Email;
using Microsoft.Extensions.Logging;

namespace DocVault.Infrastructure.Email;

// ACS not configured — do not log user identifiers, links, or tokens.
internal sealed class LogEmailService(ILogger<LogEmailService> logger) : IEmailService
{
  public Task SendPasswordResetAsync(string toEmail, string resetLink, CancellationToken ct = default)
  {
    logger.LogWarning(
      "PASSWORD RESET requested. Reset link redacted; configure a real email provider to deliver reset URLs securely.");
    return Task.CompletedTask;
  }

  public Task SendEmailConfirmationAsync(string toEmail, string confirmationLink, CancellationToken ct = default)
  {
    logger.LogWarning(
      "EMAIL VERIFICATION requested. Confirmation link redacted; configure a real email provider to deliver verification URLs securely.");
    return Task.CompletedTask;
  }
}
