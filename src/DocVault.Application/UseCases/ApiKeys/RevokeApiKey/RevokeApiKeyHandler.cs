using DocVault.Application.Abstractions.Cqrs;
using DocVault.Application.Abstractions.Persistence;
using DocVault.Application.Common.Results;

namespace DocVault.Application.UseCases.ApiKeys.RevokeApiKey;

public sealed class RevokeApiKeyHandler : ICommandHandler<RevokeApiKeyCommand, Result>
{
  private readonly IApiKeyRepository _repo;
  private readonly IUnitOfWork       _unitOfWork;

  public RevokeApiKeyHandler(IApiKeyRepository repo, IUnitOfWork unitOfWork)
  {
    _repo       = repo;
    _unitOfWork = unitOfWork;
  }

  public async Task<Result> HandleAsync(RevokeApiKeyCommand command, CancellationToken cancellationToken = default)
  {
    var key = await _repo.GetByIdAsync(command.Id, cancellationToken);
    if (key is null)
      return Result.Failure("API key not found.", "NOT_FOUND");

    if (!command.IsAdmin && key.UserId != command.CallerUserId)
      return Result.Failure("API key not found.", "NOT_FOUND");

    key.Revoke();
    await _repo.UpdateAsync(key, cancellationToken);
    await _unitOfWork.SaveChangesAsync(cancellationToken);
    return Result.Success();
  }
}
