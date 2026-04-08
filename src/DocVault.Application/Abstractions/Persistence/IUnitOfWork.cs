namespace DocVault.Application.Abstractions.Persistence;

/// <summary>
/// Wraps a group of database operations in a single atomic transaction.
/// </summary>
public interface IUnitOfWork
{
  /// <summary>
  /// Executes <paramref name="action"/> inside a database transaction.
  /// Commits on success; rolls back and rethrows on any exception.
  /// </summary>
  Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default);
}
