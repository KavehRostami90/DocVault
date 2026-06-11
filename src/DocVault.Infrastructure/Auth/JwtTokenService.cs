using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using DocVault.Application.Abstractions.Auth;
using DocVault.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace DocVault.Infrastructure.Auth;

public sealed class JwtTokenService : ITokenService
{
  private readonly AuthSettings _settings;
  private readonly UserManager<ApplicationUser> _users;
  private readonly DocVaultDbContext _db;

  public JwtTokenService(IOptions<AuthSettings> settings, UserManager<ApplicationUser> users, DocVaultDbContext db)
  {
    _settings = settings.Value;
    _users = users;
    _db = db;
  }

  public async Task<TokenPair> CreateTokenPairAsync(
    string userId, string email, string displayName,
    IReadOnlyList<string> roles, bool isGuest, bool isEmailVerified,
    CancellationToken ct = default)
  {
    var accessToken = BuildAccessToken(userId, email, displayName, roles, isGuest, isEmailVerified);
    var refreshExpiry = isGuest
      ? DateTimeOffset.UtcNow.AddHours(24)
      : DateTimeOffset.UtcNow.AddDays(_settings.RefreshTokenExpiryDays);

    var refresh = new RefreshToken
    {
      Token = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"),
      UserId = userId,
      ExpiresAt = refreshExpiry,
    };

    _db.RefreshTokens.Add(refresh);
    await _db.SaveChangesAsync(ct);

    return new TokenPair(accessToken, refresh.Token, _settings.AccessTokenExpiryMinutes * 60, refreshExpiry);
  }

  public async Task<TokenPair?> RotateRefreshTokenAsync(string oldToken, CancellationToken ct = default)
  {
    var stored = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.Token == oldToken, ct);
    if (stored is null || !stored.IsActive)
      return null;

    stored.RevokedAt = DateTimeOffset.UtcNow;

    var user = await _users.FindByIdAsync(stored.UserId);
    // Unverified accounts cannot keep a session alive via refresh — they must
    // verify and log in again (guests always have EmailConfirmed = true).
    if (user is null || !user.EmailConfirmed)
      return null;

    var roles = (IReadOnlyList<string>)await _users.GetRolesAsync(user);
    return await CreateTokenPairAsync(user.Id, user.Email!, user.DisplayName, roles, user.IsGuest, user.EmailConfirmed, ct);
  }

  public async Task RevokeRefreshTokenAsync(string token, CancellationToken ct = default)
  {
    var stored = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.Token == token, ct);
    if (stored is not null && stored.IsActive)
    {
      stored.RevokedAt = DateTimeOffset.UtcNow;
      await _db.SaveChangesAsync(ct);
    }
  }

  private string BuildAccessToken(string userId, string email, string displayName, IReadOnlyList<string> roles, bool isGuest, bool isEmailVerified)
  {
    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.JwtSigningKey));
    var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

    var claims = new List<Claim>
    {
      new(JwtRegisteredClaimNames.Sub, userId),
      new(JwtRegisteredClaimNames.Email, email),
      new("displayName", displayName),
      new("isGuest", isGuest.ToString().ToLowerInvariant()),
      new("emailVerified", isEmailVerified.ToString().ToLowerInvariant()),
    };

    foreach (var role in roles)
      claims.Add(new Claim(ClaimTypes.Role, role));

    var token = new JwtSecurityToken(
      issuer: _settings.JwtIssuer,
      audience: _settings.JwtAudience,
      claims: claims,
      expires: DateTime.UtcNow.AddMinutes(_settings.AccessTokenExpiryMinutes),
      signingCredentials: creds);

    return new JwtSecurityTokenHandler().WriteToken(token);
  }
}
