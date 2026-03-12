namespace DocVault.Api.Contracts.Imports;

/// <summary>
/// Payload for starting an import operation.
/// </summary>
/// <param name="DocumentId">The identifier of the already-created document to index.</param>
/// <param name="FileName">Original file name of the import source.</param>
/// <param name="StoragePath">Relative path in blob storage where the file was written.</param>
/// <param name="ContentType">MIME content type of the file.</param>
public sealed record ImportCreateRequest(Guid DocumentId, string FileName, string StoragePath, string ContentType);
