namespace DocVault.Infrastructure.Security;

public sealed class ClamAvOptions
{
    public const string Section = "ClamAv";

    public string Host           { get; init; } = "clamav";
    public int    Port           { get; init; } = 3310;
    public int    TimeoutSeconds { get; init; } = 30;
}
