using DocVault.Infrastructure.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

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
        {
          // If the schema already exists but has no migration history (e.g. created via
          // EnsureCreated or a volume that survived a migration reset), seed the history
          // table with all known migrations so MigrateAsync only applies genuinely new ones.
          await SeedMigrationHistoryIfNeededAsync(dbContext, cancellationToken);
          await dbContext.Database.MigrateAsync(cancellationToken);
        }
        else
        {
          await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        }
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
  /// Detects a database whose schema was created outside of EF migrations (no history rows)
  /// and inserts rows for every migration that is already represented by an existing table,
  /// so that <c>MigrateAsync</c> can proceed without trying to re-create them.
  /// </summary>
  private async Task SeedMigrationHistoryIfNeededAsync(DocVaultDbContext dbContext, CancellationToken cancellationToken)
  {
    var appliedMigrations = (await dbContext.Database.GetAppliedMigrationsAsync(cancellationToken)).ToList();
    if (appliedMigrations.Count > 0)
      return; // history already populated — nothing to do

    // Check whether the schema actually exists (Documents table is a reliable indicator).
    var conn = dbContext.Database.GetDbConnection();
    await conn.OpenAsync(cancellationToken);
    bool schemaExists;
    try
    {
      using var cmd = conn.CreateCommand();
      cmd.CommandText =
        "SELECT COUNT(*) FROM information_schema.tables " +
        "WHERE table_schema = 'public' AND table_name = 'Documents'";
      var result = await cmd.ExecuteScalarAsync(cancellationToken);
      schemaExists = Convert.ToInt64(result) > 0;
    }
    finally
    {
      await conn.CloseAsync();
    }

    if (!schemaExists)
      return; // fresh database — let MigrateAsync handle it normally

    _logger.LogWarning(
      "Database schema exists but migration history is empty. " +
      "Seeding migration history to avoid duplicate table errors.");

    // EF Core 10 ships with product version "10.0.0".
    const string productVersion = "10.0.0";
    var allMigrations = dbContext.Database.GetMigrations().ToList();

    await conn.OpenAsync(cancellationToken);
    try
    {
      // Ensure __EFMigrationsHistory exists before inserting.
      using (var ensureCmd = conn.CreateCommand())
      {
        ensureCmd.CommandText =
          """
          CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
              "MigrationId"    character varying(150) NOT NULL,
              "ProductVersion" character varying(32)  NOT NULL,
              CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
          );
          """;
        await ensureCmd.ExecuteNonQueryAsync(cancellationToken);
      }

      foreach (var migrationId in allMigrations)
      {
        using var insertCmd = conn.CreateCommand();
        insertCmd.CommandText =
          """
          INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
          VALUES (@id, @ver)
          ON CONFLICT DO NOTHING
          """;
        var p1 = insertCmd.CreateParameter();
        p1.ParameterName = "@id";
        p1.Value = migrationId;
        insertCmd.Parameters.Add(p1);

        var p2 = insertCmd.CreateParameter();
        p2.ParameterName = "@ver";
        p2.Value = productVersion;
        insertCmd.Parameters.Add(p2);

        await insertCmd.ExecuteNonQueryAsync(cancellationToken);
      }
    }
    finally
    {
      await conn.CloseAsync();
    }
  }
}
