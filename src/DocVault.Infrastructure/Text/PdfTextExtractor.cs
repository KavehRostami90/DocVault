using System.Text;
using DocVault.Application.Abstractions.Text;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace DocVault.Infrastructure.Text;

/// <summary>
/// Extracts plain text from PDF documents using PdfPig.
/// Text is extracted page-by-page and joined with newlines.
/// </summary>
public sealed class PdfTextExtractor : ITextExtractor
{
  /// <summary>
  /// Reads all text from a PDF stream using PdfPig and rewinds the stream to position 0.
  /// </summary>
  /// <param name="content">Readable stream containing the PDF bytes.</param>
  /// <param name="contentType">MIME content type (informational; unused by this extractor).</param>
  /// <param name="cancellationToken">Cancellation token.</param>
  /// <returns>Concatenated plain text from all pages, separated by newlines.</returns>
  public Task<string> ExtractAsync(Stream content, string contentType, CancellationToken cancellationToken = default)
  {
    using var doc = PdfDocument.Open(content, new ParsingOptions { ClipPaths = true });

    var builder = new StringBuilder();
    foreach (Page page in doc.GetPages())
    {
      cancellationToken.ThrowIfCancellationRequested();
      builder.AppendLine(page.Text);
    }

    content.Position = 0;
    return Task.FromResult(builder.ToString());
  }
}
