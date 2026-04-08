using DocVault.Application.Abstractions.Text;

namespace DocVault.Infrastructure.Text;

/// <summary>
/// Routes extraction to the appropriate <see cref="ITextExtractor"/> implementation
/// based on the document's MIME content type.
/// </summary>
/// <remarks>
/// Supported content-type mappings:
/// <list type="bullet">
///   <item><c>application/pdf</c> → <see cref="PdfTextExtractor"/></item>
///   <item><c>application/vnd.openxmlformats-officedocument.wordprocessingml.document</c> → <see cref="DocxTextExtractor"/></item>
///   <item><c>text/markdown</c>, <c>text/x-markdown</c> → <see cref="MarkdownExtractor"/></item>
///   <item>All other <c>text/*</c> types (including <c>text/plain</c>) → <see cref="PlainTextExtractor"/></item>
/// </list>
/// If no specific extractor matches, falls back to <see cref="PlainTextExtractor"/>.
/// </remarks>
public sealed class CompositeTextExtractor : ITextExtractor
{
  private static readonly Dictionary<string, ITextExtractor> _extractors = new(StringComparer.OrdinalIgnoreCase)
  {
    ["application/pdf"] = new PdfTextExtractor(),
    ["application/vnd.openxmlformats-officedocument.wordprocessingml.document"] = new DocxTextExtractor(),
    ["text/markdown"] = new MarkdownExtractor(),
    ["text/x-markdown"] = new MarkdownExtractor(),
    ["application/json"] = new PlainTextExtractor(),
  };

  private static readonly ITextExtractor _fallback = new PlainTextExtractor();

  /// <summary>
  /// Selects an extractor by <paramref name="contentType"/> and delegates extraction to it.
  /// Falls back to <see cref="PlainTextExtractor"/> for unrecognised types.
  /// </summary>
  /// <param name="content">Readable stream containing the document bytes.</param>
  /// <param name="contentType">MIME content type used to select the extractor.</param>
  /// <param name="cancellationToken">Cancellation token.</param>
  /// <returns>Extracted plain text.</returns>
  public Task<string> ExtractAsync(Stream content, string contentType, CancellationToken cancellationToken = default)
  {
    var extractor = _extractors.TryGetValue(contentType?.Trim() ?? string.Empty, out var found)
      ? found
      : _fallback;

    return extractor.ExtractAsync(content, contentType!, cancellationToken);
  }
}
