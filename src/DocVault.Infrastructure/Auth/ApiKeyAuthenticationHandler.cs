using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using DocVault.Application.Abstractions.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DocVault.Infrastructure.Auth;

/// <summary>
/// Authentication handler that validates <c>X-Api-Key</c> request headers.
/// Resolves the matching <see cref="DocVault.Domain.Auth.ApiKey"/> from the database,
/// loads the owner's roles via Identity, and builds a <see cref="ClaimsPrincipal"/>
/// that is compatible with the existing JWT-based role authorization policies.
/// </summary>
public sealed class ApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
  public const string SchemeName = "ApiKey";
  public const string HeaderName = "X-Api-Key";

  public ApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : base(options, logger, encoder)
  { }

  protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
  {
    if (!Request.Headers.TryGetValue(HeaderName, out var headerValues))
      return AuthenticateResult.NoResult();

    var rawKey = headerValues.ToString().Trim();
    if (string.IsNullOrEmpty(rawKey))
      return AuthenticateResult.NoResult();

    var keyHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawKey)))
                    .ToLowerInvariant();

    var keyRepo = Context.RequestServices.GetRequiredService<IApiKeyRepository>();
    var apiKey  = await keyRepo.GetByHashAsync(keyHash);

    if (apiKey is null || !apiKey.IsActive)
      return AuthenticateResult.Fail("Invalid or revoked API key.");

    var userManager = Context.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
    var user        = await userManager.FindByIdAsync(apiKey.UserId);

    if (user is null || !user.EmailConfirmed)
      return AuthenticateResult.Fail("User not found or email not confirmed.");

    var roles  = await userManager.GetRolesAsync(user);
    var claims = new List<Claim>
    {
      new(ClaimTypes.NameIdentifier, user.Id),
      new("sub",          user.Id),
      new("email",        user.Email!),
      new("displayName",  user.DisplayName),
      new("isGuest",      user.IsGuest.ToString().ToLowerInvariant()),
      new("emailVerified","true"),
    };
    foreach (var role in roles)
      claims.Add(new Claim(ClaimTypes.Role, role));

    var identity  = new ClaimsIdentity(claims, SchemeName);
    var principal = new ClaimsPrincipal(identity);
    var ticket    = new AuthenticationTicket(principal, SchemeName);

    // Best-effort: record last-used timestamp — do not fail auth if the write errors.
    try
    {
      apiKey.RecordUse();
      await keyRepo.UpdateAsync(apiKey);
      var unitOfWork = Context.RequestServices.GetRequiredService<IUnitOfWork>();
      await unitOfWork.SaveChangesAsync();
    }
    catch (Exception ex)
    {
      Logger.LogWarning(ex, "Failed to update LastUsedAt for API key {KeyId}.", apiKey.Id);
    }

    return AuthenticateResult.Success(ticket);
  }
}
