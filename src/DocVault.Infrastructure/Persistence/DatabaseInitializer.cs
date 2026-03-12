using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DocVault.Infrastructure.Persistence;

/// <summary>
/// Hosted service that runs EF Core migrations (relational provider) or
/// <c>EnsureCreated</c> (in-memory / non-relational provider) on application startup.
/// </summary>
public class DatabaseInitializer : IHostedService
{
  private readonly IServiceProvider _serviceProvider;
  private readonly ILogger<DatabaseInitializer> _logger;

  /// <summary>
  /// Initialises the service with the root service provider and a logger.
  /// </summary>
  /// <param name="serviceProvider">The root DI service provider used to create a scoped context.</param>
  /// <param name="logger">Logger for error and progress messages.</param>
  public DatabaseInitializer(IServiceProvider serviceProvider, ILogger<DatabaseInitializer> logger)
  {
    _serviceProvider = serviceProvider;
    _logger = logger;
  }

  /// <summary>
  /// Applies pending migrations or ensures the database schema exists.
  /// Throws on failure so the host fails fast rather than running against an uninitialised database.
  /// </summary>
  /// <param name="cancellationToken">Cancellation token.</param>
  public async Task StartAsync(CancellationToken cancellationToken)
  {
    using var scope = _serviceProvider.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<DocVaultDbContext>();

    try
    {
      if (dbContext.Database.IsRelational())
      {
        // GetMigrations() requires a relational provider — guard it here.
        var hasMigrations = dbContext.Database.GetMigrations().Any();
        if (hasMigrations)
          await dbContext.Database.MigrateAsync(cancellationToken);
        else
          await dbContext.Database.EnsureCreatedAsync(cancellationToken);
      }
      else
      {
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Database initialization failed.");
      throw;
    }
  }

  /// <summary>No-op: no cleanup required on shutdown.</summary>
  /// <param name="cancellationToken">Cancellation token.</param>
  public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
