using DocVault.Application.Abstractions.Email;
using Microsoft.Extensions.Logging;

namespace DocVault.Infrastructure.Email;

// No SMTP configured — logs the reset link so an admin can relay it to the user.
// Replace with a real IEmailService implementation (SendGrid, SMTP, etc.) for production.
internal sealed class LogEmailService(ILogger<LogEmailService> logger) : IEmailService
{
  public Task SendPasswordResetAsync(string toEmail, string resetLink, CancellationToken ct = default)
  {
    logger.LogWarning(
      "PASSWORD RESET REQUESTED for {Email}. Reset link (share with the user): {ResetLink}",
      toEmail, resetLink);

    return Task.CompletedTask;
  }
}
