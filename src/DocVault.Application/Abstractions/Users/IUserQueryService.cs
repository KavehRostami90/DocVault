namespace DocVault.Application.Abstractions.Users;

/// <summary>
/// Summary of a single user account, including their assigned roles.
/// </summary>
/// <param name="Id">Identity user identifier.</param>
/// <param name="Email">User's email address.</param>
/// <param name="DisplayName">Friendly display name.</param>
/// <param name="IsGuest">Whether the account is a temporary guest session.</param>
/// <param name="CreatedAt">When the account was created.</param>
/// <param name="Roles">Roles currently assigned to the user.</param>
public sealed record UserSummary(
  string Id,
  string? Email,
  string DisplayName,
  bool IsGuest,
  DateTimeOffset CreatedAt,
  IReadOnlyList<string> Roles);

/// <summary>
/// Aggregate counts of user accounts broken down by type.
/// </summary>
/// <param name="Total">Total number of registered accounts.</param>
/// <param name="Guests">Number of guest (anonymous) accounts.</param>
/// <param name="Admins">Number of accounts holding the Admin role.</param>
public sealed record UserCounts(int Total, int Guests, int Admins);

/// <summary>
/// Provides user-related query operations that require access to the identity store.
/// Kept as an application-layer abstraction so handlers do not take a dependency
/// on the infrastructure's <c>UserManager&lt;T&gt;</c>.
/// </summary>
public interface IUserQueryService
{
  /// <summary>
  /// Returns all user accounts with their roles pre-loaded in a single batched query.
  /// </summary>
  Task<IReadOnlyList<UserSummary>> ListAllWithRolesAsync(CancellationToken ct = default);

  /// <summary>
  /// Returns aggregate user counts (total, guests, admins).
  /// </summary>
  Task<UserCounts> GetCountsAsync(CancellationToken ct = default);
}
