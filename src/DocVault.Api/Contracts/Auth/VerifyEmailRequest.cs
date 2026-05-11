namespace DocVault.Api.Contracts.Auth;

public sealed record VerifyEmailRequest(string Email, string Token);
