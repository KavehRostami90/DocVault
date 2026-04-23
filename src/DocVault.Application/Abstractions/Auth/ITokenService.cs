namespace DocVault.Application.Abstractions.Auth;

public sealed record TokenPair(
  string AccessToken,
  string RefreshToken,
  int ExpiresInSeconds,
  DateTimeOffset RefreshTokenExpiresAt);

public interface ITokenService
{
  Task<TokenPair> CreateTokenPairAsync(
    string userId, string email, string displayName,
    IReadOnlyList<string> roles, bool isGuest,
    CancellationToken ct = default);

  Task<TokenPair?> RotateRefreshTokenAsync(string oldToken, CancellationToken ct = default);

  Task RevokeRefreshTokenAsync(string token, CancellationToken ct = default);
}
