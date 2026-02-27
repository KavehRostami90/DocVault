namespace DocVault.Application.Abstractions.Storage;

public interface IFileStorage
{
  Task WriteAsync(string path, Stream content, CancellationToken cancellationToken = default);
  Task<Stream> ReadAsync(string path, CancellationToken cancellationToken = default);
  Task DeleteAsync(string path, CancellationToken cancellationToken = default);
}
