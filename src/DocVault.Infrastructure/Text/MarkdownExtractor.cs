using DocVault.Application.Abstractions.Text;

namespace DocVault.Infrastructure.Text;

/// <summary>
/// Extracts plain text from Markdown documents by reading the raw UTF-8 content.
/// The Markdown syntax is returned as-is; no rendering or stripping is performed.
/// </summary>
public sealed class MarkdownExtractor : ITextExtractor
{
  /// <summary>
  /// Reads the Markdown stream as UTF-8 text and rewinds the stream to position 0.
  /// </summary>
  /// <param name="content">Readable stream containing the Markdown source.</param>
  /// <param name="contentType">MIME content type (informational; unused by this extractor).</param>
  /// <param name="cancellationToken">Cancellation token.</param>
  /// <returns>The raw Markdown text.</returns>
  public async Task<string> ExtractAsync(Stream content, string contentType, CancellationToken cancellationToken = default)
  {
    using var reader = new StreamReader(content, leaveOpen: true);
    var markdown = await reader.ReadToEndAsync(cancellationToken);
    content.Position = 0;
    return markdown;
  }
}
