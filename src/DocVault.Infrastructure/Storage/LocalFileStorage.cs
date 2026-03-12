using DocVault.Application.Abstractions.Storage;

namespace DocVault.Infrastructure.Storage;

/// <summary>
/// File-system backed implementation of <see cref="IFileStorage"/> for local development.
/// All files are stored under the configured root directory as opaque blobs.
/// Replace with an Azure Blob / S3 / MinIO adapter for production deployments.
/// </summary>
public sealed class LocalFileStorage : IFileStorage
{
  private readonly string _root;

  /// <summary>
  /// Initialises the storage and creates the root directory if it does not exist.
  /// </summary>
  /// <param name="root">Absolute path to the directory used as the storage root.</param>
  public LocalFileStorage(string root)
  {
    _root = root;
    Directory.CreateDirectory(_root);
  }

  /// <summary>
  /// Deletes the file at <paramref name="path"/> relative to the storage root.
  /// No-op if the file does not exist.
  /// </summary>
  /// <param name="path">Relative file path within the storage root.</param>
  /// <param name="cancellationToken">Cancellation token (unused for synchronous file I/O).</param>
  public Task DeleteAsync(string path, CancellationToken cancellationToken = default)
  {
    var fullPath = Path.Combine(_root, path);
    if (File.Exists(fullPath))
      File.Delete(fullPath);
    return Task.CompletedTask;
  }

  /// <summary>Opens the file at <paramref name="path"/> for reading.</summary>
  /// <param name="path">Relative file path within the storage root.</param>
  /// <param name="cancellationToken">Cancellation token (unused for synchronous file open).</param>
  /// <returns>A readable <see cref="Stream"/> positioned at the start of the file.</returns>
  public Task<Stream> ReadAsync(string path, CancellationToken cancellationToken = default)
  {
    var fullPath = Path.Combine(_root, path);
    Stream stream = File.OpenRead(fullPath);
    return Task.FromResult(stream);
  }

  /// <summary>
  /// Creates (or overwrites) the file at <paramref name="path"/> with the provided stream content.
  /// </summary>
  /// <param name="path">Relative file path within the storage root.</param>
  /// <param name="content">Stream whose contents are copied to the file.</param>
  /// <param name="cancellationToken">Cancellation token.</param>
  public async Task WriteAsync(string path, Stream content, CancellationToken cancellationToken = default)
  {
    var fullPath = Path.Combine(_root, path);
    using var file = File.Create(fullPath);
    await content.CopyToAsync(file, cancellationToken);
  }
}
