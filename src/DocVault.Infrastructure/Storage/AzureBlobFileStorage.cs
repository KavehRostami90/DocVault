using Azure.Storage.Blobs;
using DocVault.Application.Abstractions.Storage;

namespace DocVault.Infrastructure.Storage;

/// <summary>
/// Azure Blob Storage implementation of <see cref="IFileStorage"/>.
/// </summary>
public sealed class AzureBlobFileStorage : IFileStorage
{
  private readonly BlobContainerClient _container;

  // Container is created once on first write; subsequent writes skip the call.
  private volatile bool _containerReady;

  public AzureBlobFileStorage(string connectionString, string containerName)
  {
    var serviceClient = new BlobServiceClient(connectionString);
    _container = serviceClient.GetBlobContainerClient(containerName);
  }

  public async Task DeleteAsync(string path, CancellationToken cancellationToken = default)
  {
    var blob = _container.GetBlobClient(path);
    await blob.DeleteIfExistsAsync(cancellationToken: cancellationToken);
  }

  public async Task<Stream> ReadAsync(string path, CancellationToken cancellationToken = default)
  {
    var blob = _container.GetBlobClient(path);
    if (!await blob.ExistsAsync(cancellationToken))
      throw new FileNotFoundException($"Blob '{path}' was not found.", path);

    // DownloadStreamingAsync returns a non-seekable network stream.
    // Buffer into a MemoryStream so the ingestion pipeline can seek freely.
    var response = await blob.DownloadStreamingAsync(cancellationToken: cancellationToken);
    var ms = new MemoryStream();
    await using (response.Value.Content)
      await response.Value.Content.CopyToAsync(ms, cancellationToken);
    ms.Position = 0;
    return ms;
  }

  public async Task WriteAsync(string path, Stream content, CancellationToken cancellationToken = default)
  {
    if (!_containerReady)
    {
      await _container.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
      _containerReady = true;
    }
    var blob = _container.GetBlobClient(path);
    await blob.UploadAsync(content, overwrite: true, cancellationToken: cancellationToken);
  }
}
