using DocVault.Application.Abstractions.Auth;
using DocVault.Application.Abstractions.Email;
using DocVault.Application.Common.Results;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using ApplicationErrors = DocVault.Application.Common.Results.Errors;

namespace DocVault.Infrastructure.Auth;

/// <summary>
/// Wraps ASP.NET Core Identity's <see cref="UserManager{T}"/> behind the application-layer
/// <see cref="IUserService"/> abstraction so no layer above Infrastructure ever touches
/// identity-specific types directly.
/// </summary>
public sealed class IdentityUserService : IUserService
{
  private readonly UserManager<ApplicationUser> _users;
  private readonly IEmailService _email;
  private readonly AuthSettings _authSettings;

  public IdentityUserService(UserManager<ApplicationUser> users, IEmailService email, IOptions<AuthSettings> authSettings)
  {
    _users = users;
    _email = email;
    _authSettings = authSettings.Value;
  }

  public async Task<Result<UserProfile>> RegisterAsync(string email, string password, string? displayName, CancellationToken ct = default)
  {
    if (await _users.FindByEmailAsync(email) is not null)
      return Result<UserProfile>.Failure("Email already registered.");

    var user = new ApplicationUser
    {
      UserName      = email,
      Email         = email,
      DisplayName   = displayName ?? string.Empty,
      EmailConfirmed = true,
    };

    var result = await _users.CreateAsync(user, password);
    if (!result.Succeeded)
      return Result<UserProfile>.Failure(JoinErrors(result));

    await _users.AddToRoleAsync(user, AppRoles.User);
    return Result<UserProfile>.Success(await BuildProfileAsync(user));
  }

  public async Task<Result<UserProfile>> LoginAsync(string email, string password, CancellationToken ct = default)
  {
    var user = await _users.FindByEmailAsync(email);
    if (user is null || !await _users.CheckPasswordAsync(user, password))
      return Result<UserProfile>.Failure("Invalid email or password.");

    return Result<UserProfile>.Success(await BuildProfileAsync(user));
  }

  public async Task<Result<UserProfile>> CreateGuestAsync(CancellationToken ct = default)
  {
    var guestEmail = $"guest_{Guid.NewGuid():N}@docvault.guest";
    var user = new ApplicationUser
    {
      UserName      = guestEmail,
      Email         = guestEmail,
      DisplayName   = "Guest",
      IsGuest       = true,
      EmailConfirmed = true,
    };

    var result = await _users.CreateAsync(user, Guid.NewGuid().ToString("N") + "Aa1!");
    if (!result.Succeeded)
      return Result<UserProfile>.Failure("Failed to create guest session.");

    await _users.AddToRoleAsync(user, AppRoles.Guest);
    return Result<UserProfile>.Success(await BuildProfileAsync(user));
  }

  public async Task<UserProfile?> GetByIdAsync(string userId, CancellationToken ct = default)
  {
    var user = await _users.FindByIdAsync(userId);
    return user is null ? null : await BuildProfileAsync(user);
  }

  public async Task<Result> UpdateDisplayNameAsync(string userId, string displayName, CancellationToken ct = default)
  {
    var user = await _users.FindByIdAsync(userId);
    if (user is null)
      return Result.Failure(ApplicationErrors.NotFound);

    user.DisplayName = displayName;
    var result = await _users.UpdateAsync(user);
    return result.Succeeded ? Result.Success() : Result.Failure(JoinErrors(result));
  }

  public async Task<Result> ChangePasswordAsync(string userId, string currentPassword, string newPassword, CancellationToken ct = default)
  {
    var user = await _users.FindByIdAsync(userId);
    if (user is null || user.IsGuest)
      return Result.Failure(ApplicationErrors.NotFound);

    var result = await _users.ChangePasswordAsync(user, currentPassword, newPassword);
    return result.Succeeded ? Result.Success() : Result.Failure(JoinErrors(result));
  }

  public async Task<Result> AdminResetPasswordAsync(string userId, string newPassword, CancellationToken ct = default)
  {
    var user = await _users.FindByIdAsync(userId);
    if (user is null)
      return Result.Failure(ApplicationErrors.NotFound);

    await _users.RemovePasswordAsync(user);
    var result = await _users.AddPasswordAsync(user, newPassword);
    return result.Succeeded ? Result.Success() : Result.Failure(JoinErrors(result));
  }

  public async Task<bool> SendPasswordResetEmailAsync(string email, CancellationToken ct = default)
  {
    var user = await _users.FindByEmailAsync(email);
    if (user is null || user.IsGuest)
      return false;

    var token = await _users.GeneratePasswordResetTokenAsync(user);
    var link  = $"{_authSettings.FrontendBaseUrl.TrimEnd('/')}/reset-password" +
                $"?token={Uri.EscapeDataString(token)}&email={Uri.EscapeDataString(user.Email!)}";

    await _email.SendPasswordResetAsync(user.Email!, link, ct);
    return true;
  }

  public async Task<Result> ResetPasswordAsync(string email, string token, string newPassword, CancellationToken ct = default)
  {
    var user = await _users.FindByEmailAsync(email);
    if (user is null || user.IsGuest)
      return Result.Failure("Invalid or expired reset link.");

    var result = await _users.ResetPasswordAsync(user, token, newPassword);
    return result.Succeeded
      ? Result.Success()
      : Result.Failure(result.Errors.FirstOrDefault()?.Description ?? "Invalid or expired reset link.");
  }

  private async Task<UserProfile> BuildProfileAsync(ApplicationUser user)
  {
    var roles = await _users.GetRolesAsync(user);
    return new UserProfile(user.Id, user.Email!, user.DisplayName, roles.ToList(), user.IsGuest, user.CreatedAt);
  }

  private static string JoinErrors(IdentityResult result)
    => string.Join("; ", result.Errors.Select(e => e.Description));
}
