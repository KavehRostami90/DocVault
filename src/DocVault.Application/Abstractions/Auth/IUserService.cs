using DocVault.Application.Common.Results;

namespace DocVault.Application.Abstractions.Auth;

/// <summary>
/// Application-layer profile returned by all user management operations.
/// Decouples the API and Application layers from the Infrastructure identity types.
/// </summary>
public sealed record UserProfile(
  string Id,
  string Email,
  string DisplayName,
  IReadOnlyList<string> Roles,
  bool IsGuest,
  DateTimeOffset CreatedAt);

/// <summary>
/// Abstracts all user lifecycle operations so no layer above Infrastructure
/// ever touches <c>UserManager&lt;T&gt;</c> or identity-specific types directly.
/// </summary>
public interface IUserService
{
  Task<Result<UserProfile>> RegisterAsync(string email, string password, string? displayName, CancellationToken ct = default);
  Task<Result<UserProfile>> LoginAsync(string email, string password, CancellationToken ct = default);
  Task<Result<UserProfile>> CreateGuestAsync(CancellationToken ct = default);
  Task<UserProfile?> GetByIdAsync(string userId, CancellationToken ct = default);
  Task<Result> UpdateDisplayNameAsync(string userId, string displayName, CancellationToken ct = default);
  Task<Result> ChangePasswordAsync(string userId, string currentPassword, string newPassword, CancellationToken ct = default);
  Task<Result> AdminResetPasswordAsync(string userId, string newPassword, CancellationToken ct = default);
  Task<bool> SendPasswordResetEmailAsync(string email, CancellationToken ct = default);
  Task<Result> ResetPasswordAsync(string email, string token, string newPassword, CancellationToken ct = default);
}
