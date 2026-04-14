using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Pgvector.EntityFrameworkCore;

namespace DocVault.Infrastructure.Persistence;

/// <summary>
/// Used only by EF Core design-time tools (migrations add / update / script).
/// Reads the connection string from the DOCVAULT_DB environment variable,
/// falling back to a local dev default.
/// </summary>
internal sealed class DocVaultDbContextFactory : IDesignTimeDbContextFactory<DocVaultDbContext>
{
  /// <summary>
  /// Creates a <see cref="DocVaultDbContext"/> configured for the design-time tools.
  /// Reads the connection string from the <c>DOCVAULT_DB</c> environment variable,
  /// falling back to a local development default.
  /// </summary>
  /// <param name="args">Command-line arguments passed by the EF tooling (unused).</param>
  /// <returns>A fully configured <see cref="DocVaultDbContext"/> instance.</returns>
  public DocVaultDbContext CreateDbContext(string[] args)
  {
    var connectionString =
      Environment.GetEnvironmentVariable("DOCVAULT_DB")
      ?? "Host=localhost;Port=5432;Database=docvault;Username=docvault;Password=docvault";

    var options = new DbContextOptionsBuilder<DocVaultDbContext>()
      .UseNpgsql(connectionString, o => o.UseVector())
      .Options;

    return new DocVaultDbContext(options);
  }
}
