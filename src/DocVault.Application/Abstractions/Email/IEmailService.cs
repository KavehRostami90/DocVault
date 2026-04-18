namespace DocVault.Application.Abstractions.Email;

public interface IEmailService
{
  Task SendPasswordResetAsync(string toEmail, string resetLink, CancellationToken ct = default);
}
