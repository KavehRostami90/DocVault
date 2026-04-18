using DocVault.Application.Abstractions.Text;

namespace DocVault.Infrastructure.Text;

/// <summary>
/// Routes extraction to the appropriate <see cref="ITextExtractor"/> implementation
/// based on the document's MIME content type.
/// </summary>
/// <remarks>
/// Supported content-type mappings:
/// <list type="bullet">
///   <item><c>application/pdf</c> → <see cref="PdfOcrExtractor"/> (text layer first, OCR fallback for image-only pages)</item>
///   <item><c>application/vnd.openxmlformats-officedocument.wordprocessingml.document</c> → <see cref="DocxTextExtractor"/></item>
///   <item><c>text/markdown</c>, <c>text/x-markdown</c> → <see cref="MarkdownExtractor"/></item>
///   <item><c>image/png</c>, <c>image/jpeg</c>, <c>image/gif</c>, <c>image/tiff</c>, <c>image/bmp</c>, <c>image/webp</c> → <see cref="ImageOcrExtractor"/></item>
///   <item>All other <c>text/*</c> types (including <c>text/plain</c>) → <see cref="PlainTextExtractor"/></item>
/// </list>
/// If no specific extractor matches, falls back to <see cref="PlainTextExtractor"/>.
/// </remarks>
public sealed class CompositeTextExtractor : ITextExtractor
{
  private static readonly HashSet<string> _imageTypes = new(StringComparer.OrdinalIgnoreCase)
  {
    "image/png", "image/jpeg", "image/gif", "image/tiff", "image/bmp", "image/webp",
  };

  private static readonly Dictionary<string, ITextExtractor> _staticExtractors = new(StringComparer.OrdinalIgnoreCase)
  {
    ["application/vnd.openxmlformats-officedocument.wordprocessingml.document"] = new DocxTextExtractor(),
    ["text/markdown"] = new MarkdownExtractor(),
    ["text/x-markdown"] = new MarkdownExtractor(),
    ["application/json"] = new PlainTextExtractor(),
  };

  private static readonly ITextExtractor _fallback = new PlainTextExtractor();

  private readonly ImageOcrExtractor _imageExtractor;
  private readonly PdfOcrExtractor _pdfExtractor;

  public CompositeTextExtractor(ImageOcrExtractor imageExtractor, PdfOcrExtractor pdfExtractor)
  {
    _imageExtractor = imageExtractor;
    _pdfExtractor = pdfExtractor;
  }

  /// <summary>
  /// Selects an extractor by <paramref name="contentType"/> and delegates extraction to it.
  /// PDFs are routed to <see cref="PdfOcrExtractor"/> (text layer first, OCR fallback).
  /// Image types are routed to <see cref="ImageOcrExtractor"/>.
  /// Falls back to <see cref="PlainTextExtractor"/> for unrecognised types.
  /// </summary>
  public Task<string> ExtractAsync(Stream content, string contentType, CancellationToken cancellationToken = default)
  {
    var ct = contentType?.Trim() ?? string.Empty;

    if (ct.Equals("application/pdf", StringComparison.OrdinalIgnoreCase))
      return _pdfExtractor.ExtractAsync(content, ct, cancellationToken);

    if (_imageTypes.Contains(ct))
      return _imageExtractor.ExtractAsync(content, ct, cancellationToken);

    var extractor = _staticExtractors.TryGetValue(ct, out var found) ? found : _fallback;
    return extractor.ExtractAsync(content, ct, cancellationToken);
  }
}
