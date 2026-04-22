namespace DocVault.Application.Abstractions.Persistence;

/// <summary>
/// Controls when tracked changes are flushed to the database.
/// Repositories only stage changes; callers decide when to persist.
/// </summary>
public interface IUnitOfWork
{
  /// <summary>
  /// Flushes all staged changes to the database in a single round-trip.
  /// Use this for single-operation writes that do not require a transaction.
  /// </summary>
  Task SaveChangesAsync(CancellationToken cancellationToken = default);

  /// <summary>
  /// Executes <paramref name="action"/> inside a database transaction, then saves
  /// and commits. Rolls back and rethrows on any exception.
  /// </summary>
  Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default);
}
