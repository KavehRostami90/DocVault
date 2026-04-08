using DocVault.Application.Abstractions.Persistence;
using DocVault.Application.Abstractions.Users;
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
/// Handles <see cref="GetAdminStatsQuery"/> by aggregating user counts from
/// <see cref="IUserQueryService"/> and document counts from <see cref="IDocumentRepository"/>.
/// All data is fetched here — no computation is performed in the API layer.
/// </summary>
public sealed class GetAdminStatsHandler
{
  private readonly IDocumentRepository _documents;
  private readonly IUserQueryService _users;

  public GetAdminStatsHandler(IDocumentRepository documents, IUserQueryService users)
  {
    _documents = documents;
    _users     = users;
  }

  /// <summary>
  /// Builds and returns an <see cref="AdminStatsDto"/> combining live user and document counts.
  /// </summary>
  public async Task<Result<AdminStatsDto>> HandleAsync(
    GetAdminStatsQuery query,
    CancellationToken cancellationToken = default)
  {
    var userCounts     = await _users.GetCountsAsync(cancellationToken);
    var countsByStatus = await _documents.GetCountsByStatusAsync(cancellationToken);
    var totalDocuments = countsByStatus.Values.Sum();

    var dto = new AdminStatsDto(
      TotalUsers:       userCounts.Total,
      GuestUsers:       userCounts.Guests,
      RegisteredUsers:  userCounts.Total - userCounts.Guests,
      AdminUsers:       userCounts.Admins,
      TotalDocuments:   totalDocuments,
      DocumentsByStatus: countsByStatus);

    return Result<AdminStatsDto>.Success(dto);
  }
}
