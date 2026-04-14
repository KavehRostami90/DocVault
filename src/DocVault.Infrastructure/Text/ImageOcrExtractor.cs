using DocVault.Application.Abstractions.Text;

namespace DocVault.Infrastructure.Text;

/// <summary>
/// Extracts text from image files (PNG, JPEG, GIF, TIFF, BMP, WebP) using OCR.
/// Delegates recognition to <see cref="IOcrEngine"/>.
/// </summary>
public sealed class ImageOcrExtractor : ITextExtractor
{
    private readonly IOcrEngine _ocrEngine;

    public ImageOcrExtractor(IOcrEngine ocrEngine) => _ocrEngine = ocrEngine;

    /// <summary>
    /// Reads the image stream into memory, runs OCR via <see cref="IOcrEngine"/>,
    /// and rewinds the stream to position 0.
    /// </summary>
    public async Task<string> ExtractAsync(Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        using var ms = new MemoryStream();
        await content.CopyToAsync(ms, cancellationToken);
        var bytes = ms.ToArray();

        if (content.CanSeek)
            content.Position = 0;

        return await _ocrEngine.RecognizeAsync(bytes, cancellationToken);
    }
}
