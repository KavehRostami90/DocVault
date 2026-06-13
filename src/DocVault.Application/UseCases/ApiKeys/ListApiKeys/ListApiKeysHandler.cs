using DocVault.Application.Abstractions.Cqrs;
using DocVault.Application.Abstractions.Persistence;
using DocVault.Application.Common.Results;

namespace DocVault.Application.UseCases.ApiKeys.ListApiKeys;

public sealed class ListApiKeysHandler : IQueryHandler<ListApiKeysQuery, Result<IReadOnlyList<ApiKeyDto>>>
{
  private readonly IApiKeyRepository _repo;

  public ListApiKeysHandler(IApiKeyRepository repo) => _repo = repo;

  public async Task<Result<IReadOnlyList<ApiKeyDto>>> HandleAsync(
    ListApiKeysQuery query, CancellationToken cancellationToken = default)
  {
    var keys = await _repo.GetByUserIdAsync(query.UserId, cancellationToken);
    IReadOnlyList<ApiKeyDto> dtos = keys
      .Select(k => new ApiKeyDto(k.Id, k.Name, k.KeyPrefix, k.IsRevoked, k.ExpiresAt, k.LastUsedAt, k.CreatedAt))
      .ToList();
    return Result<IReadOnlyList<ApiKeyDto>>.Success(dtos);
  }
}
