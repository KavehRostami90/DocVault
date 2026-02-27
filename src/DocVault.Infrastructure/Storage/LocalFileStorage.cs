using DocVault.Application.Abstractions.Storage;

namespace DocVault.Infrastructure.Storage;

public sealed class LocalFileStorage : IFileStorage
{
  private readonly string _root;

  public LocalFileStorage(string root)
  {
    _root = root;
    Directory.CreateDirectory(_root);
  }

  public async Task DeleteAsync(string path, CancellationToken cancellationToken = default)
  {
    var fullPath = Path.Combine(_root, path);
    if (File.Exists(fullPath))
    {
      File.Delete(fullPath);
    }
    await Task.CompletedTask;
  }

  public Task<Stream> ReadAsync(string path, CancellationToken cancellationToken = default)
  {
    var fullPath = Path.Combine(_root, path);
    Stream stream = File.OpenRead(fullPath);
    return Task.FromResult(stream);
  }

  public async Task WriteAsync(string path, Stream content, CancellationToken cancellationToken = default)
  {
    var fullPath = Path.Combine(_root, path);
    using var file = File.Create(fullPath);
    await content.CopyToAsync(file, cancellationToken);
  }
}
