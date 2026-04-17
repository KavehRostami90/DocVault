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
  public async Task<string> ExtractAsync(Stream content, string contentType, CancellationToken cancellationToken = default)
  {
    // PdfPig requires a seekable stream. Azure Blob download streams are not seekable,
    // so buffer into a MemoryStream when needed.
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
      var builder = new StringBuilder();
      foreach (Page page in doc.GetPages())
      {
        cancellationToken.ThrowIfCancellationRequested();
        builder.AppendLine(page.Text);
      }
      return builder.ToString();
    }
    finally
    {
      if (disposeSeekable)
        await seekable.DisposeAsync();
    }
  }
}
