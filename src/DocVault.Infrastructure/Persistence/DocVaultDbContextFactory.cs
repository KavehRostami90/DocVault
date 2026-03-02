using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DocVault.Infrastructure.Persistence;

/// <summary>
/// Used only by EF Core design-time tools (migrations add / update / script).
/// Reads the connection string from the DOCVAULT_DB environment variable,
/// falling back to a local dev default.
/// </summary>
internal sealed class DocVaultDbContextFactory : IDesignTimeDbContextFactory<DocVaultDbContext>
{
  public DocVaultDbContext CreateDbContext(string[] args)
  {
    var connectionString =
      Environment.GetEnvironmentVariable("DOCVAULT_DB")
      ?? "Host=localhost;Port=5432;Database=docvault;Username=docvault;Password=docvault";

    var options = new DbContextOptionsBuilder<DocVaultDbContext>()
      .UseNpgsql(connectionString)
      .Options;

    return new DocVaultDbContext(options);
  }
}
