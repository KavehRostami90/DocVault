using DocVault.Application.Abstractions.Persistence;
using DocVault.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DocVault.Infrastructure;

/// <summary>
/// EF Core implementation of <see cref="IUnitOfWork"/>.
/// Repositories stage changes via the EF change tracker; this class decides when to flush.
/// Falls back to a no-op scope when the provider does not support transactions (e.g. in-memory DB).
/// </summary>
public sealed class EfUnitOfWork : IUnitOfWork
{
  private readonly DocVaultDbContext _db;

  public EfUnitOfWork(DocVaultDbContext db) => _db = db;

  /// <inheritdoc />
  public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    => _db.SaveChangesAsync(cancellationToken);

  /// <inheritdoc />
  public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default)
  {
    if (!_db.Database.IsRelational())
    {
      await action(cancellationToken);
      await _db.SaveChangesAsync(cancellationToken);
      return;
    }

    await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
    try
    {
      await action(cancellationToken);
      await _db.SaveChangesAsync(cancellationToken);
      await tx.CommitAsync(cancellationToken);
    }
    catch
    {
      await tx.RollbackAsync(cancellationToken);
      throw;
    }
  }
}
