using DocVault.Application.Abstractions.Cqrs;
using DocVault.Application.Common.Results;

namespace DocVault.Application.UseCases.ApiKeys.ListApiKeys;

public sealed record ListApiKeysQuery(string UserId) : IQuery<Result<IReadOnlyList<ApiKeyDto>>>;

public sealed record ApiKeyDto(
  Guid Id,
  string Name,
  string KeyPrefix,
  bool IsRevoked,
  DateTimeOffset? ExpiresAt,
  DateTimeOffset? LastUsedAt,
  DateTimeOffset CreatedAt);
