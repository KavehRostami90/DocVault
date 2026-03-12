using DocVault.Application.Abstractions.Text;

namespace DocVault.Infrastructure.Text;

/// <summary>
/// Extracts plain text from plain-text documents (e.g. <c>.txt</c> files) by
/// reading the stream as UTF-8. Rewinds the stream after reading.
/// </summary>
public sealed class PlainTextExtractor : ITextExtractor
{
  /// <summary>
  /// Reads the stream as UTF-8 text and rewinds the stream to position 0.
  /// </summary>
  /// <param name="content">Readable stream containing the plain text.</param>
  /// <param name="contentType">MIME content type (informational; unused by this extractor).</param>
  /// <param name="cancellationToken">Cancellation token.</param>
  /// <returns>The full plain-text content of the stream.</returns>
  public async Task<string> ExtractAsync(Stream content, string contentType, CancellationToken cancellationToken = default)
  {
    using var reader = new StreamReader(content, leaveOpen: true);
    var text = await reader.ReadToEndAsync(cancellationToken);
    content.Position = 0;
    return text;
  }
}
