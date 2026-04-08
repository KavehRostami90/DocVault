using System.Net;
using System.Net.Http.Json;
using DocVault.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DocVault.IntegrationTests.Api;

/// <summary>
/// Integration tests verifying authentication and authorisation enforcement.
/// Tests that protected endpoints return 401 and admin-only endpoints return 403
/// when called by insufficiently-privileged principals.
/// </summary>
[Collection("DocVault Integration Tests")]
public sealed class AuthorizationTests : IClassFixture<DocVaultFactory>
{
    private readonly DocVaultFactory _factory;

    public AuthorizationTests(DocVaultFactory factory) => _factory = factory;

    // -------------------------------------------------------------------------
    // Anonymous (no auth) → 401
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("/api/v1/documents")]
    [InlineData("/api/v1/search/documents")]
    [InlineData("/api/v1/tags")]
    public async Task ProtectedEndpoints_WithNoAuth_Return401(string path)
    {
        // Create a client that bypasses the TestAuthHandler override so requests
        // arrive genuinely unauthenticated.
        using var factory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                // Remove the test auth override so real JWT validation runs
                services.Configure<AuthenticationOptions>(opts =>
                {
                    // Empty scheme name → GetSchemeAsync returns null → auth middleware skips
                    // authentication, leaving an anonymous user.
                    opts.DefaultAuthenticateScheme = string.Empty;
                    // Must point to a real registered scheme so the authorization middleware
                    // can challenge the anonymous user and return 401 instead of throwing.
                    opts.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                });
            }));

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        HttpResponseMessage response;
        if (path == "/api/v1/search/documents")
            response = await client.PostAsJsonAsync(path, new { query = "test", page = 1, size = 10 });
        else
            response = await client.GetAsync(path);

        Assert.True(
            response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden,
            $"Expected 401/403 for {path} but got {response.StatusCode}");
    }

    // -------------------------------------------------------------------------
    // Regular user hitting admin endpoints → 403
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("GET",    "/api/v1/admin/stats")]
    [InlineData("GET",    "/api/v1/admin/users")]
    [InlineData("GET",    "/api/v1/admin/documents")]
    public async Task AdminEndpoints_WithUserRole_Return403(string method, string path)
    {
        // Factory already signs every request in as a regular User (not Admin).
        var client = _factory.CreateClient();

        var request = new HttpRequestMessage(new HttpMethod(method), path);
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // -------------------------------------------------------------------------
    // Login endpoint — valid credentials flow
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Login_WithWrongPassword_Returns401()
    {
        var client = _factory.CreateClient();

        var payload = new { email = "nonexistent@docvault.test", password = "WrongPassword1!" };
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", payload);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
