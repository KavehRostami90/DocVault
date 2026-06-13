namespace DocVault.Application.Abstractions.Storage;

/// <summary>Lightweight description of a single blob returned by <see cref="IFileStorage.ListAsync"/>.</summary>
public sealed record BlobInfo(string Name, DateTimeOffset LastModified);

public interface IFileStorage
{
  Task WriteAsync(string path, Stream content, CancellationToken cancellationToken = default);
  Task<Stream> ReadAsync(string path, CancellationToken cancellationToken = default);
  Task DeleteAsync(string path, CancellationToken cancellationToken = default);

  /// <summary>Returns metadata for every blob currently held in storage.</summary>
  Task<IReadOnlyList<BlobInfo>> ListAsync(CancellationToken cancellationToken = default);
}
