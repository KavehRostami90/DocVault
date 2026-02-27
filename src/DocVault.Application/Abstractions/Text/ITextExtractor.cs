namespace DocVault.Application.Abstractions.Text;

public interface ITextExtractor
{
  Task<string> ExtractAsync(Stream content, string contentType, CancellationToken cancellationToken = default);
}
