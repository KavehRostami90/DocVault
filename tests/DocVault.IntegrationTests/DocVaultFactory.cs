using DocVault.Application.Abstractions.Storage;
using DocVault.Application.Background.Queue;
using DocVault.Infrastructure.Persistence;
using DocVault.Infrastructure.Storage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DocVault.IntegrationTests;

/// <summary>
/// Spins up the real API pipeline with:
///   - EF Core InMemory database (isolated per test run)
///   - LocalFileStorage pointing at an isolated temp directory
/// No Docker / Postgres required.
/// </summary>
public sealed class DocVaultFactory : WebApplicationFactory<Program>
{
  private readonly string _storageRoot = Path.Combine(Path.GetTempPath(), $"docvault-tests-{Guid.NewGuid()}");

  // Capture the DB name once so every DbContext within this factory shares
  // the same InMemory store — essential for cross-request and seed + query flows.
  private readonly string _dbName = $"integration-{Guid.NewGuid()}";

  protected override void ConfigureWebHost(IWebHostBuilder builder)
  {
    builder.UseEnvironment("Testing");

    builder.ConfigureServices(services =>
    {
      // Remove all DbContext-related registrations added by AddDbContextFactory/AddDbContext
      // so the Npgsql-backed factory and options cannot survive into the InMemory test run.
      var toRemove = services
        .Where(sd =>
          sd.ServiceType == typeof(DbContextOptions<DocVaultDbContext>) ||
          sd.ServiceType == typeof(DbContext) ||
          (sd.ServiceType.IsGenericType &&
           sd.ServiceType.GetGenericTypeDefinition() == typeof(IDbContextOptionsConfiguration<>)) ||
          (sd.ServiceType.IsGenericType &&
           sd.ServiceType.GetGenericTypeDefinition() == typeof(IDbContextFactory<>)))
        .ToList();

      foreach (var sd in toRemove)
        services.Remove(sd);

      // Remove the scoped DocVaultDbContext lambda registered via AddScoped(sp => factory.Create…).
      services.RemoveAll<DocVaultDbContext>();

      // Remove the PostgresWorkQueue singleton so the durable queue doesn't try to hit Postgres.
      services.RemoveAll<IWorkQueue<IndexingWorkItem>>();

      // Register InMemory factory (gives both IDbContextFactory<T> and scoped DbContext).
      services.AddDbContextFactory<DocVaultDbContext>(
        opts => opts.UseInMemoryDatabase(_dbName),
        ServiceLifetime.Scoped);

      // Re-register scoped DbContext consumed by repositories and DatabaseInitializer.
      services.AddScoped(sp =>
        sp.GetRequiredService<IDbContextFactory<DocVaultDbContext>>().CreateDbContext());

      // Restore the lightweight in-process queue; channel-backed, no Postgres needed.
      services.AddSingleton<IWorkQueue<IndexingWorkItem>, ChannelWorkQueue<IndexingWorkItem>>();

      // Replace file storage with an isolated temp directory.
      services.RemoveAll<IFileStorage>();
      Directory.CreateDirectory(_storageRoot);
      services.AddSingleton<IFileStorage>(_ => new LocalFileStorage(_storageRoot));
    });
  }

  protected override void Dispose(bool disposing)
  {
    if (disposing && Directory.Exists(_storageRoot))
    {
      CleanupStorageDirectory();
    }

    base.Dispose(disposing);
  }

  private void CleanupStorageDirectory()
  {
    try
    {
      // Dispose any file storage services that might have open handles
      var serviceScope = Services.CreateScope();
      try
      {
        var fileStorage = serviceScope.ServiceProvider.GetService<IFileStorage>();
        if (fileStorage is IDisposable disposableStorage)
        {
          disposableStorage.Dispose();
        }
      }
      finally
      {
        serviceScope.Dispose();
      }

      // Force garbage collection to help release any remaining file handles
      GC.Collect();
      GC.WaitForPendingFinalizers();
      GC.Collect();

      // Retry deletion with exponential backoff
      const int MAX_RETRY_ATTEMPTS = 5;
      const int BASE_DELAY_MS = 100;

      for (var attempt = 1; attempt <= MAX_RETRY_ATTEMPTS; attempt++)
      {
        try
        {
          if (Directory.Exists(_storageRoot))
          {
            Directory.Delete(_storageRoot, recursive: true);
          }
          break; // Success, exit retry loop
        }
        catch (IOException) when (attempt < MAX_RETRY_ATTEMPTS)
        {
          // Wait with exponential backoff before retrying
          var delayMs = BASE_DELAY_MS * (int)Math.Pow(2, attempt - 1);
          Thread.Sleep(delayMs);
        }
        catch (UnauthorizedAccessException) when (attempt < MAX_RETRY_ATTEMPTS)
        {
          // Handle access issues, wait and retry
          var delayMs = BASE_DELAY_MS * (int)Math.Pow(2, attempt - 1);
          Thread.Sleep(delayMs);
        }
      }
    }
    catch
    {
      // Silently ignore cleanup failures in test disposal
      // The temp directory will be cleaned up by OS eventually
    }
  }
}

