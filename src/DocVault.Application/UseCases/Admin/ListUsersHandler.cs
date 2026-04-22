using DocVault.Application.Abstractions.Cqrs;
using DocVault.Application.Abstractions.Users;
using DocVault.Application.Common.Results;

namespace DocVault.Application.UseCases.Admin;

public sealed record ListUsersQuery : IQuery<Result<IReadOnlyList<UserSummary>>>;

public sealed class ListUsersHandler : IQueryHandler<ListUsersQuery, Result<IReadOnlyList<UserSummary>>>
{
  private readonly IUserQueryService _users;

  public ListUsersHandler(IUserQueryService users)
  {
    _users = users;
  }

  public async Task<Result<IReadOnlyList<UserSummary>>> HandleAsync(ListUsersQuery query, CancellationToken cancellationToken = default)
  {
    var users = await _users.ListAllWithRolesAsync(cancellationToken);
    return Result<IReadOnlyList<UserSummary>>.Success(users);
  }
}
