using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DocVault.Infrastructure.Persistence;

public class DatabaseInitializer : IHostedService
{
  private readonly IServiceProvider _serviceProvider;
  private readonly ILogger<DatabaseInitializer> _logger;

  public DatabaseInitializer(IServiceProvider serviceProvider, ILogger<DatabaseInitializer> logger)
  {
    _serviceProvider = serviceProvider;
    _logger = logger;
  }

  public async Task StartAsync(CancellationToken cancellationToken)
  {
    using var scope = _serviceProvider.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<DocVaultDbContext>();

    try
    {
      var hasMigrations = dbContext.Database.GetMigrations().Any();

      if (dbContext.Database.IsRelational() && hasMigrations)
      {
        await dbContext.Database.MigrateAsync(cancellationToken);
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

  public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
