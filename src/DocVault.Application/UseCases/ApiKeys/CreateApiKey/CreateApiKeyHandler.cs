using System.Security.Cryptography;
using System.Text;
using DocVault.Application.Abstractions.Cqrs;
using DocVault.Application.Abstractions.Persistence;
using DocVault.Application.Common.Results;
using DocVault.Domain.Auth;

namespace DocVault.Application.UseCases.ApiKeys.CreateApiKey;

public sealed class CreateApiKeyHandler : ICommandHandler<CreateApiKeyCommand, Result<CreateApiKeyResult>>
{
  private readonly IApiKeyRepository _repo;
  private readonly IUnitOfWork       _unitOfWork;

  public CreateApiKeyHandler(IApiKeyRepository repo, IUnitOfWork unitOfWork)
  {
    _repo       = repo;
    _unitOfWork = unitOfWork;
  }

  public async Task<Result<CreateApiKeyResult>> HandleAsync(
    CreateApiKeyCommand command, CancellationToken cancellationToken = default)
  {
    if (string.IsNullOrWhiteSpace(command.Name))
      return Result<CreateApiKeyResult>.Failure("Name is required.");

    // Generate a random key with a recognisable prefix.
    // Format: dvk_<43-char base64url> — 47 chars total, ~192 bits of entropy.
    var rawBytes = RandomNumberGenerator.GetBytes(32);
    var rawKey   = "dvk_" + Convert.ToBase64String(rawBytes)
                              .Replace('+', '-').Replace('/', '_').TrimEnd('=');
    var keyHash  = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawKey)))
                     .ToLowerInvariant();
    var prefix   = rawKey[..12];

    var key = new ApiKey
    {
      UserId    = command.UserId,
      Name      = command.Name.Trim(),
      KeyHash   = keyHash,
      KeyPrefix = prefix,
      ExpiresAt = command.ExpiresAt,
    };

    await _repo.AddAsync(key, cancellationToken);
    await _unitOfWork.SaveChangesAsync(cancellationToken);

    return Result<CreateApiKeyResult>.Success(
      new CreateApiKeyResult(key.Id, key.Name, rawKey, key.KeyPrefix, key.ExpiresAt, key.CreatedAt));
  }
}
