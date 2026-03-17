using DocVault.Application.Abstractions.Storage;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DocVault.Infrastructure.Health;

/// <summary>
/// Verifies that the file storage layer is reachable and writable by writing
/// a tiny probe blob and immediately deleting it.  A failure here means the
/// API can receive uploads but the indexing pipeline would be unable to
/// persist or read document binaries.
/// </summary>
public sealed class StorageHealthCheck : IHealthCheck
{
  private const string ProbeFile = ".health_probe";
  private readonly IFileStorage _storage;

  public StorageHealthCheck(IFileStorage storage)
    => _storage = storage;

  public async Task<HealthCheckResult> CheckHealthAsync(
    HealthCheckContext context,
    CancellationToken cancellationToken = default)
  {
    try
    {
      using var probe = new MemoryStream([0x01]);
      await _storage.WriteAsync(ProbeFile, probe, cancellationToken);
      await _storage.DeleteAsync(ProbeFile, cancellationToken);
      return HealthCheckResult.Healthy();
    }
    catch (Exception ex)
    {
      return HealthCheckResult.Unhealthy("Storage check threw an exception.", ex);
    }
  }
}
