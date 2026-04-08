using DocVault.Application.Abstractions.Persistence;
using DocVault.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DocVault.Infrastructure;

/// <summary>
/// EF Core implementation of <see cref="IUnitOfWork"/>.
/// Falls back to a no-op scope when the provider does not support transactions
/// (e.g. the in-memory database used in tests).
/// </summary>
public sealed class EfUnitOfWork : IUnitOfWork
{
  private readonly DocVaultDbContext _db;

  /// <summary>Initialises the unit of work with the current scoped DbContext.</summary>
  public EfUnitOfWork(DocVaultDbContext db) => _db = db;

  /// <inheritdoc />
  public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default)
  {
    if (!_db.Database.IsRelational())
    {
      // In-memory provider does not support transactions — execute directly.
      await action(cancellationToken);
      return;
    }

    await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
    try
    {
      await action(cancellationToken);
      await tx.CommitAsync(cancellationToken);
    }
    catch
    {
      await tx.RollbackAsync(cancellationToken);
      throw;
    }
  }
}
