using DocVault.Application.Abstractions.Persistence;
using DocVault.Application.Common.Results;

namespace DocVault.Application.UseCases.Admin;

/// <summary>
/// Projection of the admin statistics returned to the API layer.
/// </summary>
/// <param name="TotalUsers">Total number of user accounts.</param>
/// <param name="GuestUsers">Number of guest (anonymous) user accounts.</param>
/// <param name="RegisteredUsers">Number of fully-registered (non-guest) user accounts.</param>
/// <param name="AdminUsers">Number of users that hold the Admin role.</param>
/// <param name="TotalDocuments">Total number of documents across all users and statuses.</param>
/// <param name="DocumentsByStatus">Document counts keyed by processing status name.</param>
public sealed record AdminStatsDto(
  int TotalUsers,
  int GuestUsers,
  int RegisteredUsers,
  int AdminUsers,
  long TotalDocuments,
  Dictionary<string, long> DocumentsByStatus);

/// <summary>
/// Handles <see cref="GetAdminStatsQuery"/> by combining user counts (supplied by
/// the caller) with document counts fetched from the repository.
/// </summary>
/// <remarks>
/// User counts are passed as explicit parameters rather than being resolved here
/// because <c>UserManager&lt;ApplicationUser&gt;</c> belongs to the Infrastructure layer
/// and cannot be injected into Application handlers.
/// </remarks>
public sealed class GetAdminStatsHandler
{
  private readonly IDocumentRepository _documents;

  public GetAdminStatsHandler(IDocumentRepository documents)
  {
    _documents = documents;
  }

  /// <summary>
  /// Builds and returns an <see cref="AdminStatsDto"/> combining the supplied user
  /// counts with live document counts from the repository.
  /// </summary>
  /// <param name="query">The query (currently a marker record with no data).</param>
  /// <param name="totalUsers">Total number of user accounts.</param>
  /// <param name="guestUsers">Number of guest user accounts.</param>
  /// <param name="registeredUsers">Number of fully-registered (non-guest) user accounts.</param>
  /// <param name="adminUsers">Number of users that hold the Admin role.</param>
  /// <param name="cancellationToken">Cancellation token.</param>
  /// <returns>A successful <see cref="Result{T}"/> wrapping the stats DTO.</returns>
  public async Task<Result<AdminStatsDto>> HandleAsync(
    GetAdminStatsQuery query,
    int totalUsers,
    int guestUsers,
    int registeredUsers,
    int adminUsers,
    CancellationToken cancellationToken = default)
  {
    var countsByStatus = await _documents.GetCountsByStatusAsync(cancellationToken);
    var total = countsByStatus.Values.Sum();

    var dto = new AdminStatsDto(
      TotalUsers: totalUsers,
      GuestUsers: guestUsers,
      RegisteredUsers: registeredUsers,
      AdminUsers: adminUsers,
      TotalDocuments: total,
      DocumentsByStatus: countsByStatus);

    return Result<AdminStatsDto>.Success(dto);
  }
}
