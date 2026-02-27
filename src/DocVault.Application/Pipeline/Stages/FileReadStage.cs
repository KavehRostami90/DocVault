using DocVault.Application.Abstractions.Storage;

namespace DocVault.Application.Pipeline.Stages;

public sealed class FileReadStage
{
  private readonly IFileStorage _storage;

  public FileReadStage(IFileStorage storage)
  {
    _storage = storage;
  }

  public Task<Stream> ReadAsync(string path, CancellationToken cancellationToken = default)
    => _storage.ReadAsync(path, cancellationToken);
}
