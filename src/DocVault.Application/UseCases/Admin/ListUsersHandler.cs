using DocVault.Application.Abstractions.Users;
using DocVault.Application.Common.Results;

namespace DocVault.Application.UseCases.Admin;

/// <summary>Marker record — no parameters needed for this query.</summary>
public sealed record ListUsersQuery;

/// <summary>
/// Handles <see cref="ListUsersQuery"/> by delegating to <see cref="IUserQueryService"/>,
/// which batches the role lookup to avoid N+1 queries.
/// </summary>
public sealed class ListUsersHandler
{
  private readonly IUserQueryService _users;

  public ListUsersHandler(IUserQueryService users)
  {
    _users = users;
  }

  /// <summary>Returns all users with their roles in a single batched call.</summary>
  public async Task<Result<IReadOnlyList<UserSummary>>> HandleAsync(
    ListUsersQuery query,
    CancellationToken ct = default)
  {
    var users = await _users.ListAllWithRolesAsync(ct);
    return Result<IReadOnlyList<UserSummary>>.Success(users);
  }
}
