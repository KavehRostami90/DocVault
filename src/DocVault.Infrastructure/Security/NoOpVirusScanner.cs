using DocVault.Application.Abstractions.Security;

namespace DocVault.Infrastructure.Security;

/// <summary>
/// Used when ClamAV is not configured. Every file is considered clean.
/// </summary>
public sealed class NoOpVirusScanner : IVirusScanner
{
    public Task<VirusScanResult> ScanAsync(Stream content, CancellationToken cancellationToken = default)
        => Task.FromResult(new VirusScanResult(IsClean: true));
}
