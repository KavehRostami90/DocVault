using DocVault.Domain.Common;

namespace DocVault.Api.Composition;

public sealed class UploadOptions
{
  public const string Section = "Upload";

  public long MaxFileSizeBytes { get; init; } = ValidationConstants.Documents.MAX_FILE_SIZE_BYTES;
}
