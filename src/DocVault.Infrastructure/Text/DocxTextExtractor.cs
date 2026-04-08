using System.Text;
using DocVault.Application.Abstractions.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocVault.Infrastructure.Text;

/// <summary>
/// Extracts plain text from Word Open XML (.docx) documents using DocumentFormat.OpenXml.
/// Only the main document body is extracted (headers, footers and comments are excluded).
/// </summary>
public sealed class DocxTextExtractor : ITextExtractor
{
  /// <summary>
  /// Opens a .docx stream, reads all paragraph text from the main document body,
  /// and rewinds the stream to position 0.
  /// </summary>
  /// <param name="content">Readable stream containing the .docx bytes.</param>
  /// <param name="contentType">MIME content type (informational; unused by this extractor).</param>
  /// <param name="cancellationToken">Cancellation token.</param>
  /// <returns>Plain text extracted from the document body, one paragraph per line.</returns>
  public Task<string> ExtractAsync(Stream content, string contentType, CancellationToken cancellationToken = default)
  {
    using var doc = WordprocessingDocument.Open(content, isEditable: false);

    var body = doc.MainDocumentPart?.Document?.Body;
    if (body is null)
      return Task.FromResult(string.Empty);

    var builder = new StringBuilder();
    foreach (var paragraph in body.Descendants<Paragraph>())
    {
      cancellationToken.ThrowIfCancellationRequested();
      var text = string.Concat(paragraph.Descendants<DocumentFormat.OpenXml.Wordprocessing.Text>().Select(t => t.Text));
      if (!string.IsNullOrWhiteSpace(text))
        builder.AppendLine(text);
    }

    content.Position = 0;
    return Task.FromResult(builder.ToString());
  }
}
