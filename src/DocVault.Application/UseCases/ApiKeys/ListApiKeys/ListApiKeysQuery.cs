namespace DocVault.Application.UseCases.ApiKeys.ListApiKeys;

public sealed record ListApiKeysQuery(string UserId);

public sealed record ApiKeyDto(
  Guid Id,
  string Name,
  string KeyPrefix,
  bool IsRevoked,
  DateTimeOffset? ExpiresAt,
  DateTimeOffset? LastUsedAt,
  DateTimeOffset CreatedAt);
