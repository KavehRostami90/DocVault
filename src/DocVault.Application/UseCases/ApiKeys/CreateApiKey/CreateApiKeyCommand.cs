namespace DocVault.Application.UseCases.ApiKeys.CreateApiKey;

public sealed record CreateApiKeyCommand(
  string UserId,
  string Name,
  DateTimeOffset? ExpiresAt);

public sealed record CreateApiKeyResult(
  Guid Id,
  string Name,
  string RawKey,
  string KeyPrefix,
  DateTimeOffset? ExpiresAt,
  DateTimeOffset CreatedAt);
