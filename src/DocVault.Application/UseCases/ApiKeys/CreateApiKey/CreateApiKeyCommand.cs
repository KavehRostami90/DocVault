using DocVault.Application.Abstractions.Cqrs;
using DocVault.Application.Common.Results;

namespace DocVault.Application.UseCases.ApiKeys.CreateApiKey;

public sealed record CreateApiKeyCommand(
  string UserId,
  string Name,
  DateTimeOffset? ExpiresAt) : ICommand<Result<CreateApiKeyResult>>;

public sealed record CreateApiKeyResult(
  Guid Id,
  string Name,
  string RawKey,
  string KeyPrefix,
  DateTimeOffset? ExpiresAt,
  DateTimeOffset CreatedAt);
