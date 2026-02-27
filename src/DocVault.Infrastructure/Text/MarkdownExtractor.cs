using DocVault.Application.Abstractions.Text;

namespace DocVault.Infrastructure.Text;

public sealed class MarkdownExtractor : ITextExtractor
{
  public async Task<string> ExtractAsync(Stream content, string contentType, CancellationToken cancellationToken = default)
  {
    using var reader = new StreamReader(content, leaveOpen: true);
    var markdown = await reader.ReadToEndAsync(cancellationToken);
    content.Position = 0;
    return markdown;
  }
}
