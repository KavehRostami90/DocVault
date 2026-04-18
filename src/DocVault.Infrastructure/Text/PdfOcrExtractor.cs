using System.Text;
using DocVault.Application.Abstractions.Text;
using Microsoft.Extensions.Logging;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace DocVault.Infrastructure.Text;

/// <summary>
/// Extracts text from PDF documents using a two-pass strategy:
/// 1. Native text extraction via PdfPig (fast, accurate for text-layer PDFs).
/// 2. OCR fallback via <see cref="IOcrEngine"/> for pages whose text layer is empty
///    (e.g. scanned / image-only PDFs).  Each embedded image on a text-free page is
///    individually passed to the OCR engine and the results are concatenated.
/// </summary>
public sealed class PdfOcrExtractor : ITextExtractor
{
    private readonly IOcrEngine _ocrEngine;
    private readonly ILogger<PdfOcrExtractor> _logger;

    public PdfOcrExtractor(IOcrEngine ocrEngine, ILogger<PdfOcrExtractor> logger)
    {
        _ocrEngine = ocrEngine;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<string> ExtractAsync(Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        // PdfPig requires a seekable stream; buffer when the source is not seekable.
        Stream seekable;
        bool disposeSeekable;
        if (content.CanSeek)
        {
            seekable = content;
            disposeSeekable = false;
        }
        else
        {
            seekable = new MemoryStream();
            await content.CopyToAsync(seekable, cancellationToken);
            seekable.Position = 0;
            disposeSeekable = true;
        }

        try
        {
            using var doc = PdfDocument.Open(seekable, new ParsingOptions { ClipPaths = true });
            var sb = new StringBuilder();
            int ocrPageCount = 0;

            foreach (Page page in doc.GetPages())
            {
                cancellationToken.ThrowIfCancellationRequested();

                var pageText = page.Text?.Trim();
                if (!string.IsNullOrWhiteSpace(pageText))
                {
                    sb.AppendLine(pageText);
                    continue;
                }

                // Page has no text layer — attempt OCR on embedded images.
                var images = page.GetImages().ToList();
                if (images.Count == 0)
                    continue;

                ocrPageCount++;
                foreach (var img in images)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    byte[]? bytes = null;
                    if (img.TryGetPng(out var png))
                        bytes = png;
                    else
                    {
                        var raw = img.RawBytes.ToArray();
                        if (raw.Length > 0)
                            bytes = raw;
                    }

                    if (bytes is null || bytes.Length == 0)
                        continue;

                    var ocrText = await _ocrEngine.RecognizeAsync(bytes, cancellationToken);
                    if (!string.IsNullOrWhiteSpace(ocrText))
                        sb.AppendLine(ocrText);
                }
            }

            if (ocrPageCount > 0)
                _logger.LogInformation("PDF OCR fallback applied to {PageCount} image-only page(s).", ocrPageCount);

            return sb.ToString();
        }
        finally
        {
            if (disposeSeekable)
                await seekable.DisposeAsync();
        }
    }
}
