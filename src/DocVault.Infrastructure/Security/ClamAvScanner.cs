using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;
using DocVault.Application.Abstractions.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DocVault.Infrastructure.Security;

/// <summary>
/// Scans file content using a ClamAV daemon via the INSTREAM protocol (TCP, port 3310).
/// The daemon never writes the file to disk — data is streamed over the socket.
/// </summary>
public sealed class ClamAvScanner : IVirusScanner
{
    private static readonly byte[] InstreamCommand = "zINSTREAM\0"u8.ToArray();

    private readonly string        _host;
    private readonly int           _port;
    private readonly TimeSpan      _timeout;
    private readonly ILogger<ClamAvScanner> _logger;

    public ClamAvScanner(IOptions<ClamAvOptions> options, ILogger<ClamAvScanner> logger)
    {
        _host    = options.Value.Host;
        _port    = options.Value.Port;
        _timeout = TimeSpan.FromSeconds(options.Value.TimeoutSeconds);
        _logger  = logger;
    }

    public async Task<VirusScanResult> ScanAsync(Stream content, CancellationToken cancellationToken = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(_timeout);

        using var client = new TcpClient();
        await client.ConnectAsync(_host, _port, cts.Token);
        await using var socket = client.GetStream();

        // Send INSTREAM command
        await socket.WriteAsync(InstreamCommand, cts.Token);

        // Stream file in 4 KB chunks: [4-byte big-endian length][chunk data]
        var buffer      = new byte[4096];
        var lengthBytes = new byte[4];
        int read;

        while ((read = await content.ReadAsync(buffer, cts.Token)) > 0)
        {
            BinaryPrimitives.WriteUInt32BigEndian(lengthBytes, (uint)read);
            await socket.WriteAsync(lengthBytes, cts.Token);
            await socket.WriteAsync(buffer.AsMemory(0, read), cts.Token);
        }

        // Zero-length chunk signals end of stream
        await socket.WriteAsync(new byte[4], cts.Token);

        // Read response (max 256 bytes, null-terminated)
        var responseBuffer = new byte[256];
        var responseLen    = await socket.ReadAsync(responseBuffer, cts.Token);
        var response       = Encoding.UTF8.GetString(responseBuffer, 0, responseLen).TrimEnd('\0', '\n');

        _logger.LogDebug("ClamAV response: {Response}", response);

        if (response.EndsWith("OK", StringComparison.Ordinal))
            return new VirusScanResult(IsClean: true);

        if (response.Contains("FOUND", StringComparison.Ordinal))
        {
            var threat = response
                .Replace("stream: ", "", StringComparison.Ordinal)
                .Replace(" FOUND", "", StringComparison.Ordinal);
            _logger.LogWarning("ClamAV detected threat: {Threat}", threat);
            return new VirusScanResult(IsClean: false, ThreatName: threat);
        }

        throw new InvalidOperationException($"Unexpected ClamAV response: {response}");
    }
}
