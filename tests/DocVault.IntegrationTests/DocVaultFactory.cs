using DocVault.Application.Abstractions.Storage;
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

  protected override void ConfigureWebHost(IWebHostBuilder builder)
  {
    builder.UseEnvironment("Testing");

    builder.ConfigureServices(services =>
    {
      // Remove both the cached options AND the IDbContextOptionsConfiguration
      // callbacks that call UseNpgsql — this prevents the dual-provider conflict
      // when InMemory is added afterwards.
      var toRemove = services
        .Where(sd =>
          sd.ServiceType == typeof(DbContextOptions<DocVaultDbContext>) ||
          (sd.ServiceType.IsGenericType &&
           sd.ServiceType.GetGenericTypeDefinition() == typeof(IDbContextOptionsConfiguration<>)))
        .ToList();

      foreach (var sd in toRemove)
        services.Remove(sd);

      services.AddDbContext<DocVaultDbContext>(opts =>
        opts.UseInMemoryDatabase($"integration-{Guid.NewGuid()}"));

      // Replace file storage with an isolated temp directory.
      services.RemoveAll<IFileStorage>();
      Directory.CreateDirectory(_storageRoot);
      services.AddSingleton<IFileStorage>(_ => new LocalFileStorage(_storageRoot));
    });
  }

  protected override void Dispose(bool disposing)
  {
    base.Dispose(disposing);
    if (disposing && Directory.Exists(_storageRoot))
      Directory.Delete(_storageRoot, recursive: true);
  }
}