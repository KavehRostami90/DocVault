using System.Security.Claims;
using System.Text.Encodings.Web;
using DocVault.Infrastructure.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DocVault.IntegrationTests.Infrastructure;

/// <summary>
/// Authentication handler for integration tests that need Admin-level access.
/// Signs every request in as an admin user, bypassing real JWT validation.
/// </summary>
public sealed class TestAdminAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public static readonly Guid AdminUserId = Guid.Parse("00000000-0000-0000-0000-000000000002");
    public const string AdminUserEmail = "admin@docvault.test";

    public TestAdminAuthHandler(
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
            new Claim(ClaimTypes.NameIdentifier, AdminUserId.ToString()),
            new Claim(ClaimTypes.Email, AdminUserEmail),
            new Claim(ClaimTypes.Role, AppRoles.Admin),
            new Claim(ClaimTypes.Role, AppRoles.User),
        };

        var identity  = new ClaimsIdentity(claims, TestAuthHandler.SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket    = new AuthenticationTicket(principal, TestAuthHandler.SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
