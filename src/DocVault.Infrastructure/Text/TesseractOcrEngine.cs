using DocVault.Application.Abstractions.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tesseract;

namespace DocVault.Infrastructure.Text;

/// <summary>
/// OCR engine backed by the native Tesseract library.
/// A single <see cref="TesseractEngine"/> instance is held for the lifetime of this
/// singleton and protected by a semaphore because the native engine is not thread-safe.
/// </summary>
public sealed class TesseractOcrEngine : IOcrEngine, IDisposable
{
    private readonly TesseractEngine? _engine;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly ILogger<TesseractOcrEngine> _logger;

    public TesseractOcrEngine(IOptions<OcrOptions> options, ILogger<TesseractOcrEngine> logger)
    {
        _logger = logger;
        var opts = options.Value;
        try
        {
            _engine = new TesseractEngine(opts.TessDataPath, opts.Language, EngineMode.Default);
            _logger.LogInformation("Tesseract OCR engine initialised (tessdata: {Path}, language: {Lang})",
                opts.TessDataPath, opts.Language);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Tesseract OCR engine could not be initialised — image OCR will return empty text. " +
                "Check that tesseract-ocr is installed and TessDataPath is correct ({Path}).",
                opts.TessDataPath);
        }
    }

    /// <inheritdoc />
    public async Task<string> RecognizeAsync(byte[] imageBytes, CancellationToken cancellationToken = default)
    {
        if (_engine is null)
            return string.Empty;

        await _lock.WaitAsync(cancellationToken);
        try
        {
            using var pix = Pix.LoadFromMemory(imageBytes);
            using var page = _engine.Process(pix);
            return page.GetText()?.Trim() ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OCR recognition failed for image of {Bytes} bytes", imageBytes.Length);
            return string.Empty;
        }
        finally
        {
            _lock.Release();
        }
    }

    public void Dispose()
    {
        _lock.Dispose();
        _engine?.Dispose();
    }
}
