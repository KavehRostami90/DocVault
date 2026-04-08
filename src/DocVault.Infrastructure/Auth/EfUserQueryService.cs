using DocVault.Application.Abstractions.Users;
using DocVault.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DocVault.Infrastructure.Auth;

/// <summary>
/// EF Core implementation of <see cref="IUserQueryService"/>.
/// Batches role lookups in two queries (users + user-role-name join) to
/// eliminate the N+1 pattern that arises from calling
/// <c>UserManager.GetRolesAsync()</c> per user.
/// </summary>
internal sealed class EfUserQueryService : IUserQueryService
{
  private readonly DocVaultDbContext _db;

  public EfUserQueryService(DocVaultDbContext db)
  {
    _db = db;
  }

  /// <inheritdoc />
  public async Task<IReadOnlyList<UserSummary>> ListAllWithRolesAsync(CancellationToken ct = default)
  {
    // Single query: join AspNetUserRoles → AspNetRoles to get (UserId, RoleName) pairs.
    var rolesByUser = await _db.UserRoles
      .Join(_db.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => new { ur.UserId, r.Name })
      .ToListAsync(ct);

    var roleMap = rolesByUser
      .GroupBy(x => x.UserId)
      .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(x => x.Name!).ToList());

    var users = await _db.Users
      .OrderByDescending(u => u.CreatedAt)
      .ToListAsync(ct);

    return users
      .Select(u => new UserSummary(
        u.Id,
        u.Email,
        u.DisplayName,
        u.IsGuest,
        u.CreatedAt,
        roleMap.TryGetValue(u.Id, out var roles) ? roles : []))
      .ToList();
  }

  /// <inheritdoc />
  public async Task<UserCounts> GetCountsAsync(CancellationToken ct = default)
  {
    var adminRoleName = AppRoles.Admin;

    var total  = await _db.Users.CountAsync(ct);
    var guests = await _db.Users.CountAsync(u => u.IsGuest, ct);
    var admins = await _db.UserRoles
      .Join(_db.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => new { ur.UserId, r.Name })
      .CountAsync(x => x.Name == adminRoleName, ct);

    return new UserCounts(total, guests, admins);
  }
}
