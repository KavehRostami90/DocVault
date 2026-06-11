using DocVault.Api.Composition;
using DocVault.Api.Contracts.Admin;
using DocVault.Api.Contracts.Auth;
using DocVault.Api.Contracts.Auth.Profile;
using DocVault.Api.Middleware;
using DocVault.Api.Validation;
using DocVault.Application.Abstractions.Auth;
using DocVault.Application.Abstractions.Persistence;

namespace DocVault.Api.Endpoints;

public static class AuthEndpoints
{
  private const string RefreshTokenCookie = "refresh_token";

  public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder routes)
  {
    var group = routes.MapGroup("/auth");

    group.MapPost("/register", async (
      RegisterRequest request,
      IUserService userService,
      CancellationToken ct) =>
    {
      var result = await userService.RegisterAsync(request.Email, request.Password, request.DisplayName, ct);
      if (!result.IsSuccess)
        return Results.Conflict(new { error = result.Error });

      // No tokens until the email is verified — the user signs in after confirming.
      return Results.Ok(new { message = "Account created. Check your inbox to verify your email, then sign in." });
    })
    .AddEndpointFilterFactory(ValidationFilter.Create<RegisterRequest>())
    .AllowAnonymous()
    .RequireRateLimiting(RateLimitPolicies.AuthEndpoints)
    .Produces(StatusCodes.Status200OK)
    .Produces(StatusCodes.Status409Conflict)
    .WithSummary("Register a new user account (sign in after verifying the email)");

    group.MapPost("/login", async (
      LoginRequest request,
      IUserService userService,
      ITokenService tokens,
      HttpContext http,
      CancellationToken ct) =>
    {
      var result = await userService.LoginAsync(request.Email, request.Password, ct);
      if (!result.IsSuccess)
        return Results.Problem(detail: result.Error, statusCode: StatusCodes.Status401Unauthorized);

      var profile = result.Value!;
      var pair = await tokens.CreateTokenPairAsync(profile.Id, profile.Email, profile.DisplayName, profile.Roles, profile.IsGuest, profile.IsEmailVerified, ct);
      SetRefreshCookie(http, pair);
      return Results.Ok(BuildResponse(profile, pair));
    })
    .AddEndpointFilterFactory(ValidationFilter.Create<LoginRequest>())
    .AllowAnonymous()
    .RequireRateLimiting(RateLimitPolicies.AuthEndpoints)
    .Produces<AuthResponse>()
    .Produces(StatusCodes.Status401Unauthorized)
    .WithSummary("Login with email and password");

    group.MapPost("/guest", async (
      IUserService userService,
      ITokenService tokens,
      HttpContext http,
      CancellationToken ct) =>
    {
      var result = await userService.CreateGuestAsync(ct);
      if (!result.IsSuccess)
        return Results.Problem("Failed to create guest session.", statusCode: StatusCodes.Status500InternalServerError);

      var profile = result.Value!;
      // Guests are ephemeral — no email to verify.
      var pair = await tokens.CreateTokenPairAsync(profile.Id, profile.Email, profile.DisplayName, profile.Roles, isGuest: true, isEmailVerified: true, ct);
      SetRefreshCookie(http, pair);
      return Results.Ok(BuildResponse(profile, pair));
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
      IUserService userService) =>
    {
      if (!currentUser.IsAuthenticated || currentUser.UserId is null)
        return Results.Unauthorized();

      var profile = await userService.GetByIdAsync(currentUser.UserId.ToString()!);
      if (profile is null)
        return Results.Unauthorized();

      return Results.Ok(ToUserInfo(profile));
    })
    .RequireAuthorization()
    .Produces<UserInfo>()
    .WithSummary("Get the current authenticated user's profile");

    group.MapGet("/me/storage", async (
      ICurrentUser currentUser,
      IDocumentRepository documents,
      CancellationToken ct) =>
    {
      if (!currentUser.IsAuthenticated || currentUser.UserId is null)
        return Results.Unauthorized();

      var (usedBytes, documentCount) = await documents.GetStorageUsedBytesAsync(currentUser.UserId.Value, ct);
      return Results.Ok(new StorageUsageResponse(usedBytes, documentCount));
    })
    .RequireAuthorization()
    .Produces<StorageUsageResponse>()
    .WithSummary("Get the current user's storage usage");

    group.MapPut("/me", async (
      UpdateProfileRequest request,
      ICurrentUser currentUser,
      IUserService userService,
      CancellationToken ct) =>
    {
      if (!currentUser.IsAuthenticated || currentUser.UserId is null)
        return Results.Unauthorized();

      var result = await userService.UpdateDisplayNameAsync(currentUser.UserId.ToString()!, request.DisplayName, ct);
      return result.IsSuccess ? Results.NoContent() : Results.ValidationProblem(
        new Dictionary<string, string[]> { ["displayName"] = [result.Error ?? "Update failed."] });
    })
    .AddEndpointFilterFactory(ValidationFilter.Create<UpdateProfileRequest>())
    .RequireAuthorization()
    .Produces(StatusCodes.Status204NoContent)
    .WithSummary("Update the current user's display name");

    group.MapPut("/me/reset-password", async (
      ResetUserPasswordRequest request,
      ICurrentUser currentUser,
      IUserService userService,
      CancellationToken ct) =>
    {
      if (!currentUser.IsAuthenticated || currentUser.UserId is null)
        return Results.Unauthorized();

      var result = await userService.AdminResetPasswordAsync(currentUser.UserId.ToString()!, request.NewPassword, ct);
      return result.IsSuccess ? Results.NoContent() : Results.Problem(
        detail: result.Error, statusCode: StatusCodes.Status422UnprocessableEntity);
    })
    .AddEndpointFilterFactory(ValidationFilter.Create<ResetUserPasswordRequest>())
    .RequireAuthorization(AuthPolicies.RequireAdmin)
    .Produces(StatusCodes.Status204NoContent)
    .WithSummary("Admin: reset own password without providing the current password");

    group.MapPut("/me/password", async (
      ChangePasswordRequest request,
      ICurrentUser currentUser,
      IUserService userService,
      CancellationToken ct) =>
    {
      if (!currentUser.IsAuthenticated || currentUser.UserId is null)
        return Results.Unauthorized();

      var result = await userService.ChangePasswordAsync(currentUser.UserId.ToString()!, request.CurrentPassword, request.NewPassword, ct);
      return result.IsSuccess ? Results.NoContent() : Results.ValidationProblem(
        new Dictionary<string, string[]> { ["password"] = [result.Error ?? "Password change failed."] });
    })
    .AddEndpointFilterFactory(ValidationFilter.Create<ChangePasswordRequest>())
    .RequireAuthorization()
    .Produces(StatusCodes.Status204NoContent)
    .Produces(StatusCodes.Status401Unauthorized)
    .WithSummary("Change the current user's password");

    group.MapPost("/forgot-password", async (
      ForgotPasswordRequest request,
      IUserService userService,
      CancellationToken ct) =>
    {
      await userService.SendPasswordResetEmailAsync(request.Email, ct);
      return Results.Ok(new { message = "If that email is registered you will receive a reset link shortly." });
    })
    .AddEndpointFilterFactory(ValidationFilter.Create<ForgotPasswordRequest>())
    .AllowAnonymous()
    .RequireRateLimiting(RateLimitPolicies.AuthEndpoints)
    .WithSummary("Request a password-reset link (sent via email / logged in dev)");

    group.MapPost("/reset-password", async (
      ResetPasswordRequest request,
      IUserService userService,
      CancellationToken ct) =>
    {
      var result = await userService.ResetPasswordAsync(request.Email, request.Token, request.NewPassword, ct);
      return result.IsSuccess ? Results.NoContent() : Results.Problem(
        detail: result.Error, statusCode: StatusCodes.Status400BadRequest);
    })
    .AddEndpointFilterFactory(ValidationFilter.Create<ResetPasswordRequest>())
    .AllowAnonymous()
    .RequireRateLimiting(RateLimitPolicies.AuthEndpoints)
    .Produces(StatusCodes.Status204NoContent)
    .Produces(StatusCodes.Status400BadRequest)
    .WithSummary("Reset a password using the token from the forgot-password email");

    group.MapPost("/verify-email", async (
      VerifyEmailRequest request,
      IUserService userService,
      CancellationToken ct) =>
    {
      var result = await userService.ConfirmEmailAsync(request.Email, request.Token, ct);
      return result.IsSuccess ? Results.NoContent() : Results.Problem(
        detail: result.Error, statusCode: StatusCodes.Status400BadRequest);
    })
    .AddEndpointFilterFactory(ValidationFilter.Create<VerifyEmailRequest>())
    .AllowAnonymous()
    .RequireRateLimiting(RateLimitPolicies.AuthEndpoints)
    .Produces(StatusCodes.Status204NoContent)
    .Produces(StatusCodes.Status400BadRequest)
    .WithSummary("Confirm email address using the token from the verification email");

    group.MapPost("/resend-verification", async (
      ResendVerificationRequest request,
      IUserService userService,
      CancellationToken ct) =>
    {
      // Always return the same response — never reveal whether the address is registered.
      await userService.SendEmailConfirmationAsync(request.Email, ct);
      return Results.Ok(new { message = "If that email is registered and unverified, a new confirmation link has been sent." });
    })
    .AddEndpointFilterFactory(ValidationFilter.Create<ResendVerificationRequest>())
    .AllowAnonymous()
    .RequireRateLimiting(RateLimitPolicies.AuthEndpoints)
    .WithSummary("Resend the email verification link");

    return routes;
  }

  private static void SetRefreshCookie(HttpContext http, TokenPair pair)
  {
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

  private static AuthResponse BuildResponse(UserProfile profile, TokenPair pair)
    => new(pair.AccessToken, pair.ExpiresInSeconds, ToUserInfo(profile));

  private static UserInfo ToUserInfo(UserProfile profile)
  {
    var primaryRole = profile.Roles.Contains("Admin") ? "Admin"
                    : profile.Roles.Contains("Guest") ? "Guest"
                    : "User";
    return new UserInfo(profile.Id, profile.Email, profile.DisplayName, primaryRole, profile.IsGuest, profile.IsEmailVerified, profile.CreatedAt);
  }
}
