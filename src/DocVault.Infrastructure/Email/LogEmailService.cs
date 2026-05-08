using DocVault.Application.Abstractions.Email;
using Microsoft.Extensions.Logging;

namespace DocVault.Infrastructure.Email;

// ACS not configured — logs links to console so a developer can relay them manually.
internal sealed class LogEmailService(ILogger<LogEmailService> logger) : IEmailService
{
  public Task SendPasswordResetAsync(string toEmail, string resetLink, CancellationToken ct = default)
  {
    logger.LogWarning(
      "PASSWORD RESET for {Email} — share this link: {ResetLink}",
      toEmail, resetLink);
    return Task.CompletedTask;
  }

  public Task SendEmailConfirmationAsync(string toEmail, string confirmationLink, CancellationToken ct = default)
  {
    logger.LogWarning(
      "EMAIL VERIFICATION for {Email} — share this link: {ConfirmationLink}",
      toEmail, confirmationLink);
    return Task.CompletedTask;
  }
}
