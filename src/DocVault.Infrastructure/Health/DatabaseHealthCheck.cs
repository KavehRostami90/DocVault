using DocVault.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DocVault.Infrastructure.Health;

/// <summary>
/// Verifies that the application can reach the database by calling
/// <see cref="Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade.CanConnectAsync"/>.
/// Works with both the PostgreSQL provider (real TCP probe) and the in-memory provider
/// (always returns <see langword="true"/>).
/// </summary>
public sealed class DatabaseHealthCheck : IHealthCheck
{
  private readonly IServiceScopeFactory _scopeFactory;

  public DatabaseHealthCheck(IServiceScopeFactory scopeFactory)
    => _scopeFactory = scopeFactory;

  public async Task<HealthCheckResult> CheckHealthAsync(
    HealthCheckContext context,
    CancellationToken cancellationToken = default)
  {
    try
    {
      using var scope = _scopeFactory.CreateScope();
      var ctx = scope.ServiceProvider.GetRequiredService<DocVaultDbContext>();
      var canConnect = await ctx.Database.CanConnectAsync(cancellationToken);

      return canConnect
        ? HealthCheckResult.Healthy()
        : HealthCheckResult.Unhealthy("Cannot connect to the database.");
    }
    catch (Exception ex)
    {
      return HealthCheckResult.Unhealthy("Database check threw an exception.", ex);
    }
  }
}
