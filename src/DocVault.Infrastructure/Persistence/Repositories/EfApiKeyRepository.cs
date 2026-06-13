using DocVault.Application.Abstractions.Persistence;
using DocVault.Domain.Auth;
using Microsoft.EntityFrameworkCore;

namespace DocVault.Infrastructure.Persistence.Repositories;

public sealed class EfApiKeyRepository : IApiKeyRepository
{
  private readonly DocVaultDbContext _db;

  public EfApiKeyRepository(DocVaultDbContext db) => _db = db;

  public async Task AddAsync(ApiKey key, CancellationToken ct = default)
    => await _db.ApiKeys.AddAsync(key, ct);

  public Task<ApiKey?> GetByHashAsync(string keyHash, CancellationToken ct = default)
    => _db.ApiKeys.FirstOrDefaultAsync(k => k.KeyHash == keyHash, ct);

  public Task<ApiKey?> GetByIdAsync(Guid id, CancellationToken ct = default)
    => _db.ApiKeys.FirstOrDefaultAsync(k => k.Id == id, ct);

  public async Task<IReadOnlyList<ApiKey>> GetByUserIdAsync(string userId, CancellationToken ct = default)
    => await _db.ApiKeys
        .Where(k => k.UserId == userId)
        .OrderByDescending(k => k.CreatedAt)
        .ToListAsync(ct);

  public Task UpdateAsync(ApiKey key, CancellationToken ct = default)
  {
    // Do NOT call _db.ApiKeys.Update(key) here. Update() marks every column as Modified
    // regardless of what actually changed. Both callers (RevokeApiKeyHandler and
    // ApiKeyAuthenticationHandler) always load the entity from this same DbContext, so
    // the entity is already tracked. EF's change tracker records only the mutated columns
    // (IsRevoked or LastUsedAt), preventing a concurrent RecordUse() from writing a stale
    // IsRevoked = false back over a Revoke() that committed after the key was read.
    return Task.CompletedTask;
  }

  public async Task DeleteAsync(Guid id, CancellationToken ct = default)
  {
    var key = await _db.ApiKeys.FindAsync([id], ct);
    if (key is not null) _db.ApiKeys.Remove(key);
  }
}
