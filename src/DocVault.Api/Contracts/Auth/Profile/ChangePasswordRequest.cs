namespace DocVault.Api.Contracts.Auth.Profile;

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
