using DocVault.Application.Abstractions.Text;

namespace DocVault.Application.Pipeline.Stages;

public sealed class TextExtractStage
{
  private readonly ITextExtractor _extractor;

  public TextExtractStage(ITextExtractor extractor)
  {
    _extractor = extractor;
  }

  public Task<string> ExtractAsync(Stream content, string contentType, CancellationToken cancellationToken = default)
    => _extractor.ExtractAsync(content, contentType, cancellationToken);
}
