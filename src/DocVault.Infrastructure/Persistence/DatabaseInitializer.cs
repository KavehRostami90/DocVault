using DocVault.Infrastructure.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace DocVault.Infrastructure.Persistence;

/// <summary>
/// Hosted service that runs EF Core migrations (relational provider) or
/// <c>EnsureCreated</c> (in-memory / non-relational provider) on application startup,
/// then seeds roles and the default admin user.
/// </summary>
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
      if (dbContext.Database.IsRelational())
      {
        var hasMigrations = dbContext.Database.GetMigrations().Any();
        if (hasMigrations)
          await MigrateWithFallbackAsync(dbContext, cancellationToken);
        else
          await dbContext.Database.EnsureCreatedAsync(cancellationToken);
      }
      else
      {
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);
      }

      var seeder = scope.ServiceProvider.GetRequiredService<IdentitySeeder>();
      await seeder.SeedAsync(cancellationToken);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Database initialization failed.");
      throw;
    }
  }

  public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

  /// <summary>
  /// Runs <c>MigrateAsync</c> and, if a duplicate-table error is raised (meaning the schema
  /// was pre-created without a migration history), seeds the history table from a fresh
  /// connection and retries so only genuinely new migrations are applied.
  /// </summary>
  private async Task MigrateWithFallbackAsync(DocVaultDbContext dbContext, CancellationToken cancellationToken)
  {
    try
    {
      await dbContext.Database.MigrateAsync(cancellationToken);
    }
    catch (PostgresException ex) when (ex.SqlState == "42P07") // duplicate_table
    {
      _logger.LogWarning(
        "Schema exists but EF migration history is missing (42P07 – relation already exists). " +
        "Seeding history table with all known migrations and retrying.");

      await SeedHistoryFromExistingSchemaAsync(dbContext, cancellationToken);
      await dbContext.Database.MigrateAsync(cancellationToken);
    }
  }

  /// <summary>
  /// Opens a dedicated <see cref="NpgsqlConnection"/> (bypassing EF's connection management)
  /// and inserts every known migration ID into <c>__EFMigrationsHistory</c>, creating the
  /// table first when it does not yet exist.
  /// </summary>
  private static async Task SeedHistoryFromExistingSchemaAsync(
    DocVaultDbContext dbContext,
    CancellationToken cancellationToken)
  {
    var connectionString = dbContext.Database.GetConnectionString()!;
    const string productVersion = "10.0.0";
    var allMigrations = dbContext.Database.GetMigrations().ToList();

    await using var conn = new NpgsqlConnection(connectionString);
    await conn.OpenAsync(cancellationToken);

    await using (var ensureCmd = conn.CreateCommand())
    {
      ensureCmd.CommandText =
        """
        CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
            "MigrationId"    character varying(150) NOT NULL,
            "ProductVersion" character varying(32)  NOT NULL,
            CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
        )
        """;
      await ensureCmd.ExecuteNonQueryAsync(cancellationToken);
    }

    foreach (var migrationId in allMigrations)
    {
      await using var insertCmd = conn.CreateCommand();
      insertCmd.CommandText =
        """
        INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
        VALUES (@id, @ver)
        ON CONFLICT DO NOTHING
        """;
      insertCmd.Parameters.AddWithValue("id", migrationId);
      insertCmd.Parameters.AddWithValue("ver", productVersion);
      await insertCmd.ExecuteNonQueryAsync(cancellationToken);
    }
  }
}
