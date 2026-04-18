namespace DocVault.Api.Contracts.Auth;

public sealed record AuthResponse(
  string AccessToken,
  int ExpiresIn,
  UserInfo User);

public sealed record UserInfo(
  string Id,
  string Email,
  string DisplayName,
  string Role,
  bool IsGuest,
  DateTimeOffset CreatedAt);
