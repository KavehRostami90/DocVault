using DocVault.Api.Composition;
using DocVault.Api.Contracts.Admin;
using DocVault.Api.Contracts.Auth;
using DocVault.Api.Contracts.Auth.Profile;
using DocVault.Api.Middleware;
using DocVault.Api.Validation;
using DocVault.Application.Abstractions.Auth;
using DocVault.Application.Abstractions.Email;
using DocVault.Infrastructure.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

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
        [AppRoles.User], isGuest: false, ct);

      SetRefreshCookie(http, pair);
      return Results.Ok(BuildResponse(user, pair, AppRoles.User));
    })
    .AddEndpointFilterFactory(ValidationFilter.Create<RegisterRequest>())
    .AllowAnonymous()
    .RequireRateLimiting(RateLimitPolicies.AuthEndpoints)
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
    .RequireRateLimiting(RateLimitPolicies.AuthEndpoints)
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
        user.Id, user.Email!, "Guest", [AppRoles.Guest], isGuest: true, ct);

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
      ICurrentUser currentUser,
      UserManager<ApplicationUser> users) =>
    {
      if (!currentUser.IsAuthenticated || currentUser.UserId is null)
        return Results.Unauthorized();

      var user = await users.FindByIdAsync(currentUser.UserId.ToString()!);
      if (user is null) return Results.Unauthorized();

      var roles = await users.GetRolesAsync(user);
      var primaryRole = roles.Contains(AppRoles.Admin) ? AppRoles.Admin
        : roles.Contains(AppRoles.Guest) ? AppRoles.Guest
        : AppRoles.User;

      return Results.Ok(new UserInfo(user.Id, user.Email!, user.DisplayName, primaryRole, user.IsGuest, user.CreatedAt));
    })
    .RequireAuthorization()
    .Produces<UserInfo>()
    .WithSummary("Get the current authenticated user's profile");

    group.MapPut("/me", async (
      UpdateProfileRequest request,
      ICurrentUser currentUser,
      UserManager<ApplicationUser> users,
      CancellationToken ct) =>
    {
      if (!currentUser.IsAuthenticated || currentUser.UserId is null)
        return Results.Unauthorized();

      var user = await users.FindByIdAsync(currentUser.UserId.ToString()!);
      if (user is null) return Results.Unauthorized();

      user.DisplayName = request.DisplayName;
      var result = await users.UpdateAsync(user);
      if (!result.Succeeded)
        return Results.ValidationProblem(
          result.Errors.ToDictionary(e => e.Code, e => new[] { e.Description }));

      return Results.NoContent();
    })
    .AddEndpointFilterFactory(ValidationFilter.Create<UpdateProfileRequest>())
    .RequireAuthorization()
    .Produces(StatusCodes.Status204NoContent)
    .WithSummary("Update the current user's display name");

    group.MapPut("/me/reset-password", async (
      ResetUserPasswordRequest request,
      ICurrentUser currentUser,
      UserManager<ApplicationUser> users,
      CancellationToken ct) =>
    {
      if (!currentUser.IsAuthenticated || currentUser.UserId is null)
        return Results.Unauthorized();

      var user = await users.FindByIdAsync(currentUser.UserId.ToString()!);
      if (user is null) return Results.Unauthorized();

      await users.RemovePasswordAsync(user);
      var result = await users.AddPasswordAsync(user, request.NewPassword);

      return result.Succeeded ? Results.NoContent() : Results.Problem(
        detail: string.Join("; ", result.Errors.Select(e => e.Description)),
        statusCode: StatusCodes.Status422UnprocessableEntity);
    })
    .AddEndpointFilterFactory(ValidationFilter.Create<ResetUserPasswordRequest>())
    .RequireAuthorization(AuthPolicies.RequireAdmin)
    .Produces(StatusCodes.Status204NoContent)
    .WithSummary("Admin: reset own password without providing the current password");

    group.MapPut("/me/password", async (
      ChangePasswordRequest request,
      ICurrentUser currentUser,
      UserManager<ApplicationUser> users,
      CancellationToken ct) =>
    {
      if (!currentUser.IsAuthenticated || currentUser.UserId is null)
        return Results.Unauthorized();

      var user = await users.FindByIdAsync(currentUser.UserId.ToString()!);
      if (user is null || user.IsGuest) return Results.Unauthorized();

      var result = await users.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
      if (!result.Succeeded)
        return Results.ValidationProblem(
          result.Errors.ToDictionary(e => e.Code, e => new[] { e.Description }));

      return Results.NoContent();
    })
    .AddEndpointFilterFactory(ValidationFilter.Create<ChangePasswordRequest>())
    .RequireAuthorization()
    .Produces(StatusCodes.Status204NoContent)
    .Produces(StatusCodes.Status401Unauthorized)
    .WithSummary("Change the current user's password");

    group.MapPost("/forgot-password", async (
      ForgotPasswordRequest request,
      UserManager<ApplicationUser> users,
      IEmailService email,
      IOptions<AuthSettings> settings,
      CancellationToken ct) =>
    {
      var user = await users.FindByEmailAsync(request.Email);

      // Always return 200 — never reveal whether the email is registered.
      if (user is not null && !user.IsGuest)
      {
        var token = await users.GeneratePasswordResetTokenAsync(user);
        var link  = $"{settings.Value.FrontendBaseUrl.TrimEnd('/')}/reset-password" +
                    $"?token={Uri.EscapeDataString(token)}&email={Uri.EscapeDataString(user.Email!)}";
        await email.SendPasswordResetAsync(user.Email!, link, ct);
      }

      return Results.Ok(new { message = "If that email is registered you will receive a reset link shortly." });
    })
    .AddEndpointFilterFactory(ValidationFilter.Create<ForgotPasswordRequest>())
    .AllowAnonymous()
    .RequireRateLimiting(RateLimitPolicies.AuthEndpoints)
    .WithSummary("Request a password-reset link (sent via email / logged in dev)");

    group.MapPost("/reset-password", async (
      ResetPasswordRequest request,
      UserManager<ApplicationUser> users,
      CancellationToken ct) =>
    {
      var user = await users.FindByEmailAsync(request.Email);
      if (user is null || user.IsGuest)
        return Results.Problem("Invalid or expired reset link.", statusCode: StatusCodes.Status400BadRequest);

      var result = await users.ResetPasswordAsync(user, request.Token, request.NewPassword);
      if (!result.Succeeded)
        return Results.Problem(
          detail: result.Errors.FirstOrDefault()?.Description ?? "Invalid or expired reset link.",
          statusCode: StatusCodes.Status400BadRequest);

      return Results.NoContent();
    })
    .AddEndpointFilterFactory(ValidationFilter.Create<ResetPasswordRequest>())
    .AllowAnonymous()
    .RequireRateLimiting(RateLimitPolicies.AuthEndpoints)
    .Produces(StatusCodes.Status204NoContent)
    .Produces(StatusCodes.Status400BadRequest)
    .WithSummary("Reset a password using the token from the forgot-password email");

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
      new UserInfo(user.Id, user.Email!, user.DisplayName, role, user.IsGuest, user.CreatedAt));
}
