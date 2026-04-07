using DocVault.Api.Contracts.Auth;
using DocVault.Api.Validation;
using DocVault.Application.Abstractions.Auth;
using DocVault.Infrastructure.Auth;
using Microsoft.AspNetCore.Identity;

namespace DocVault.Api.Endpoints;

public static class AuthEndpoints
{
  private const string RefreshTokenCookie = "refresh_token";

  public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder routes)
  {
    var group = routes.MapGroup("/auth");

    group.MapPost("/register", async (
      RegisterRequest request,
      UserManager<ApplicationUser> users,
      ITokenService tokens,
      HttpContext http,
      CancellationToken ct) =>
    {
      if (await users.FindByEmailAsync(request.Email) is not null)
        return Results.Conflict(new { error = "Email already registered." });

      var user = new ApplicationUser
      {
        UserName = request.Email,
        Email = request.Email,
        DisplayName = request.DisplayName,
        EmailConfirmed = true,
      };

      var result = await users.CreateAsync(user, request.Password);
      if (!result.Succeeded)
        return Results.ValidationProblem(
          result.Errors.ToDictionary(e => e.Code, e => new[] { e.Description }));

      await users.AddToRoleAsync(user, AppRoles.User);

      var pair = await tokens.CreateTokenPairAsync(
        user.Id, user.Email!, user.DisplayName,
        [AppRoles.User], false, ct);

      SetRefreshCookie(http, pair);
      return Results.Ok(BuildResponse(user, pair, AppRoles.User));
    })
    .AddEndpointFilterFactory(ValidationFilter.Create<RegisterRequest>())
    .AllowAnonymous()
    .Produces<AuthResponse>()
    .Produces(StatusCodes.Status409Conflict)
    .WithSummary("Register a new user account");

    group.MapPost("/login", async (
      LoginRequest request,
      UserManager<ApplicationUser> users,
      ITokenService tokens,
      HttpContext http,
      CancellationToken ct) =>
    {
      var user = await users.FindByEmailAsync(request.Email);
      if (user is null || !await users.CheckPasswordAsync(user, request.Password))
        return Results.Problem(
          detail: "Invalid email or password.",
          statusCode: StatusCodes.Status401Unauthorized);

      var roles = await users.GetRolesAsync(user);
      var primaryRole = roles.Contains(AppRoles.Admin) ? AppRoles.Admin : AppRoles.User;

      var pair = await tokens.CreateTokenPairAsync(
        user.Id, user.Email!, user.DisplayName, roles, user.IsGuest, ct);

      SetRefreshCookie(http, pair);
      return Results.Ok(BuildResponse(user, pair, primaryRole));
    })
    .AddEndpointFilterFactory(ValidationFilter.Create<LoginRequest>())
    .AllowAnonymous()
    .Produces<AuthResponse>()
    .Produces(StatusCodes.Status401Unauthorized)
    .WithSummary("Login with email and password");

    group.MapPost("/guest", async (
      UserManager<ApplicationUser> users,
      ITokenService tokens,
      HttpContext http,
      CancellationToken ct) =>
    {
      var guestEmail = $"guest_{Guid.NewGuid():N}@docvault.guest";
      var user = new ApplicationUser
      {
        UserName = guestEmail,
        Email = guestEmail,
        DisplayName = "Guest",
        IsGuest = true,
        EmailConfirmed = true,
      };

      var result = await users.CreateAsync(user, Guid.NewGuid().ToString("N") + "Aa1!");
      if (!result.Succeeded)
        return Results.Problem("Failed to create guest session.", statusCode: StatusCodes.Status500InternalServerError);

      await users.AddToRoleAsync(user, AppRoles.Guest);

      var pair = await tokens.CreateTokenPairAsync(
        user.Id, user.Email!, "Guest", [AppRoles.Guest], true, ct);

      SetRefreshCookie(http, pair);
      return Results.Ok(BuildResponse(user, pair, AppRoles.Guest));
    })
    .AllowAnonymous()
    .Produces<AuthResponse>()
    .WithSummary("Start an anonymous guest session (no registration required)");

    group.MapPost("/refresh", async (
      HttpContext http,
      ITokenService tokens,
      CancellationToken ct) =>
    {
      var oldToken = http.Request.Cookies[RefreshTokenCookie];
      if (string.IsNullOrWhiteSpace(oldToken))
        return Results.Unauthorized();

      var pair = await tokens.RotateRefreshTokenAsync(oldToken, ct);
      if (pair is null)
        return Results.Unauthorized();

      SetRefreshCookie(http, pair);
      return Results.Ok(new { accessToken = pair.AccessToken, expiresIn = pair.ExpiresInSeconds });
    })
    .AllowAnonymous()
    .WithSummary("Silently refresh the access token using the httpOnly cookie");

    group.MapPost("/logout", async (
      HttpContext http,
      ITokenService tokens,
      CancellationToken ct) =>
    {
      var token = http.Request.Cookies[RefreshTokenCookie];
      if (!string.IsNullOrWhiteSpace(token))
        await tokens.RevokeRefreshTokenAsync(token, ct);

      http.Response.Cookies.Delete(RefreshTokenCookie);
      return Results.NoContent();
    })
    .RequireAuthorization()
    .WithSummary("Logout and revoke the refresh token");

    group.MapGet("/me", async (
      HttpContext http,
      UserManager<ApplicationUser> users) =>
    {
      var userId = http.User.FindFirst("sub")?.Value
                ?? http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

      if (userId is null) return Results.Unauthorized();

      var user = await users.FindByIdAsync(userId);
      if (user is null) return Results.Unauthorized();

      var roles = await users.GetRolesAsync(user);
      var primaryRole = roles.Contains(AppRoles.Admin) ? AppRoles.Admin
        : roles.Contains(AppRoles.Guest) ? AppRoles.Guest
        : AppRoles.User;

      return Results.Ok(new UserInfo(user.Id, user.Email!, user.DisplayName, primaryRole, user.IsGuest));
    })
    .RequireAuthorization()
    .Produces<UserInfo>()
    .WithSummary("Get the current authenticated user's profile");

    return routes;
  }

  private static void SetRefreshCookie(HttpContext http, TokenPair pair)
  {
    // In production: SameSite=None;Secure is required for cross-origin cookie (SWA → App Service).
    // In development: SameSite=Lax without Secure so the cookie works over HTTP through the Vite proxy.
    var isDev = http.RequestServices.GetRequiredService<IHostEnvironment>().IsDevelopment();

    http.Response.Cookies.Append(RefreshTokenCookie, pair.RefreshToken, new CookieOptions
    {
      HttpOnly = true,
      Secure   = !isDev,
      SameSite = isDev ? SameSiteMode.Lax : SameSiteMode.None,
      Expires  = pair.RefreshTokenExpiresAt,
      Path     = "/",
    });
  }

  private static AuthResponse BuildResponse(ApplicationUser user, TokenPair pair, string role) =>
    new(pair.AccessToken, pair.ExpiresInSeconds,
      new UserInfo(user.Id, user.Email!, user.DisplayName, role, user.IsGuest));
}
