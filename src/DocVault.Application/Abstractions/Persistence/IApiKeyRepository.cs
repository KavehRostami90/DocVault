using DocVault.Domain.Auth;

namespace DocVault.Application.Abstractions.Persistence;

public interface IApiKeyRepository
{
  Task AddAsync(ApiKey key, CancellationToken ct = default);
  Task<ApiKey?> GetByHashAsync(string keyHash, CancellationToken ct = default);
  Task<ApiKey?> GetByIdAsync(Guid id, CancellationToken ct = default);
  Task<IReadOnlyList<ApiKey>> GetByUserIdAsync(string userId, CancellationToken ct = default);
  Task UpdateAsync(ApiKey key, CancellationToken ct = default);
  Task DeleteAsync(Guid id, CancellationToken ct = default);
}
