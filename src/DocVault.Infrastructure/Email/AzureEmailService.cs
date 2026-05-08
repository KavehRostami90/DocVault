using Azure;
using Azure.Communication.Email;
using DocVault.Application.Abstractions.Email;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DocVault.Infrastructure.Email;

internal sealed class AzureEmailService : IEmailService
{
  private readonly EmailClient _client;
  private readonly EmailSettings _settings;
  private readonly ILogger<AzureEmailService> _logger;

  public AzureEmailService(IOptions<EmailSettings> options, ILogger<AzureEmailService> logger)
  {
    _settings = options.Value;
    _logger = logger;
    _client = new EmailClient(_settings.ConnectionString);
  }

  public Task SendPasswordResetAsync(string toEmail, string resetLink, CancellationToken ct = default)
  {
    const string subject = "Reset your DocVault password";
    var html = $"""
      <!DOCTYPE html>
      <html>
        <body style="font-family:sans-serif;max-width:560px;margin:0 auto;padding:24px">
          <h2 style="color:#1a1a1a">Password reset request</h2>
          <p>We received a request to reset the password for your DocVault account.</p>
          <p style="margin:32px 0">
            <a href="{resetLink}"
               style="background:#2563eb;color:#fff;padding:12px 24px;border-radius:6px;text-decoration:none;font-weight:600">
              Reset password
            </a>
          </p>
          <p style="color:#666;font-size:14px">
            This link expires in 24 hours. If you did not request a reset, you can safely ignore this email.
          </p>
        </body>
      </html>
      """;

    return SendAsync(toEmail, subject, html, ct);
  }

  public Task SendEmailConfirmationAsync(string toEmail, string confirmationLink, CancellationToken ct = default)
  {
    const string subject = "Verify your DocVault email address";
    var html = $"""
      <!DOCTYPE html>
      <html>
        <body style="font-family:sans-serif;max-width:560px;margin:0 auto;padding:24px">
          <h2 style="color:#1a1a1a">Confirm your email address</h2>
          <p>Thanks for signing up for DocVault! Please verify your email address to complete registration.</p>
          <p style="margin:32px 0">
            <a href="{confirmationLink}"
               style="background:#2563eb;color:#fff;padding:12px 24px;border-radius:6px;text-decoration:none;font-weight:600">
              Verify email
            </a>
          </p>
          <p style="color:#666;font-size:14px">
            This link expires in 24 hours. If you did not create a DocVault account, you can ignore this email.
          </p>
        </body>
      </html>
      """;

    return SendAsync(toEmail, subject, html, ct);
  }

  private async Task SendAsync(string toEmail, string subject, string html, CancellationToken ct)
  {
    var message = new EmailMessage(
      senderAddress: _settings.SenderAddress,
      content: new EmailContent(subject) { Html = html },
      recipients: new EmailRecipients([new EmailAddress(toEmail)]));

    // WaitUntil.Started — submit and return once ACS accepts the job; no polling needed.
    EmailSendOperation operation = await _client.SendAsync(WaitUntil.Started, message, ct);

    _logger.LogInformation(
      "ACS email submitted — to={Email} subject={Subject} operationId={OperationId}",
      toEmail, subject, operation.Id);
  }
}
