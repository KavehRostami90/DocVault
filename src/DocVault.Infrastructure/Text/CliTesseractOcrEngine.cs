using System.Diagnostics;
using DocVault.Application.Abstractions.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DocVault.Infrastructure.Text;

/// <summary>
/// OCR engine that shells out to the system <c>tesseract</c> CLI binary.
/// This avoids native library loading issues with the NuGet binding on Linux/Docker.
/// Requires <c>tesseract-ocr</c> to be installed (e.g. via apt).
/// </summary>
public sealed class CliTesseractOcrEngine : IOcrEngine
{
    private readonly ILogger<CliTesseractOcrEngine> _logger;
    private readonly string _language;
    private readonly bool _available;

    public CliTesseractOcrEngine(IOptions<OcrOptions> options, ILogger<CliTesseractOcrEngine> logger)
    {
        _logger   = logger;
        _language = options.Value.Language;
        _available = CheckAvailable();

        if (_available)
            _logger.LogInformation("Tesseract CLI OCR engine ready (language: {Lang})", _language);
        else
            _logger.LogWarning("tesseract binary not found in PATH — image OCR will return empty text. " +
                               "Install tesseract-ocr (e.g. apt-get install tesseract-ocr tesseract-ocr-eng).");
    }

    /// <inheritdoc />
    public async Task<string> RecognizeAsync(byte[] imageBytes, CancellationToken cancellationToken = default)
    {
        if (!_available || imageBytes.Length == 0)
            return string.Empty;

        var inputPath  = Path.Combine(Path.GetTempPath(), $"ocr_in_{Guid.NewGuid():N}");
        var outputBase = Path.Combine(Path.GetTempPath(), $"ocr_out_{Guid.NewGuid():N}");
        var outputTxt  = outputBase + ".txt";

        try
        {
            await File.WriteAllBytesAsync(inputPath, imageBytes, cancellationToken);

            using var proc = new Process();
            proc.StartInfo.FileName        = "tesseract";
            proc.StartInfo.UseShellExecute = false;
            proc.StartInfo.RedirectStandardError  = true;
            proc.StartInfo.RedirectStandardOutput = true;
            proc.StartInfo.CreateNoWindow  = true;
            proc.StartInfo.ArgumentList.Add(inputPath);
            proc.StartInfo.ArgumentList.Add(outputBase);
            proc.StartInfo.ArgumentList.Add("-l");
            proc.StartInfo.ArgumentList.Add(_language);

            proc.Start();

            // Read stderr before WaitForExitAsync to avoid deadlocks on large output
            var stderrTask = proc.StandardError.ReadToEndAsync(cancellationToken);
            await proc.WaitForExitAsync(cancellationToken);
            var stderr = await stderrTask;

            if (proc.ExitCode != 0)
            {
                _logger.LogWarning("Tesseract CLI exited {Code}: {Err}", proc.ExitCode, stderr);
                return string.Empty;
            }

            if (!File.Exists(outputTxt))
                return string.Empty;

            return (await File.ReadAllTextAsync(outputTxt, cancellationToken)).Trim();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Tesseract CLI OCR failed for {Bytes}-byte image", imageBytes.Length);
            return string.Empty;
        }
        finally
        {
            TryDelete(inputPath);
            TryDelete(outputTxt);
        }
    }

    /// <summary>Checks whether the <c>tesseract</c> binary is reachable at startup.</summary>
    private static bool CheckAvailable()
    {
        try
        {
            using var p = new Process();
            p.StartInfo.FileName        = "tesseract";
            p.StartInfo.Arguments       = "--version";
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.RedirectStandardError  = true;
            p.StartInfo.CreateNoWindow  = true;
            p.Start();
            p.WaitForExit(3_000);
            return p.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch { /* best-effort cleanup */ }
    }
}
