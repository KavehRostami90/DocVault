namespace DocVault.Application.Abstractions.Security;

public sealed record VirusScanResult(bool IsClean, string? ThreatName = null);

public interface IVirusScanner
{
    Task<VirusScanResult> ScanAsync(Stream content, CancellationToken cancellationToken = default);
}
