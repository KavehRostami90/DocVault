using System.Security.Claims;
using System.Text.Encodings.Web;
using DocVault.Infrastructure.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DocVault.IntegrationTests.Infrastructure;

/// <summary>
/// A no-op authentication handler for integration tests.
/// Automatically signs every incoming request as a regular User so that
/// <c>RequireAuthorization()</c> endpoints work without a real JWT.
/// </summary>
public sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
  public const string SchemeName = "Test";

  // A fixed Guid used as the test user's identity across all requests.
  public static readonly Guid TestUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");
  public const string TestUserEmail = "testuser@docvault.test";

  public TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : base(options, logger, encoder)
  {
  }

  protected override Task<AuthenticateResult> HandleAuthenticateAsync()
  {
    var claims = new[]
    {
      new Claim(ClaimTypes.NameIdentifier, TestUserId.ToString()),
      new Claim(ClaimTypes.Email, TestUserEmail),
      new Claim(ClaimTypes.Role, AppRoles.User),
    };

    var identity = new ClaimsIdentity(claims, SchemeName);
    var principal = new ClaimsPrincipal(identity);
    var ticket = new AuthenticationTicket(principal, SchemeName);

    return Task.FromResult(AuthenticateResult.Success(ticket));
  }
}
