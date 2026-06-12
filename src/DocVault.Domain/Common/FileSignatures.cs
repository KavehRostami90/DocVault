namespace DocVault.Domain.Common;

/// <summary>
/// Known file magic-byte signatures keyed by MIME type.
/// Used to verify that uploaded file content matches the declared content type.
/// Text-based types (plain, markdown, JSON) have no reliable magic bytes and are skipped.
/// </summary>
public static class FileSignatures
{
    private static readonly Dictionary<string, byte[][]> Known = new(StringComparer.OrdinalIgnoreCase)
    {
        ["application/pdf"] = [[0x25, 0x50, 0x44, 0x46]],                                          // %PDF
        ["image/png"]       = [[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]],                  // ‰PNG\r\n\x1A\n
        ["image/jpeg"]      = [[0xFF, 0xD8, 0xFF]],                                                  // JFIF / EXIF
        ["image/gif"]       = [[0x47, 0x49, 0x46, 0x38, 0x37, 0x61],                               // GIF87a
                                [0x47, 0x49, 0x46, 0x38, 0x39, 0x61]],                              // GIF89a
        ["image/bmp"]       = [[0x42, 0x4D]],                                                        // BM
        ["image/tiff"]      = [[0x49, 0x49, 0x2A, 0x00], [0x4D, 0x4D, 0x00, 0x2A]],               // II* / MM*
        ["image/webp"]      = [[0x52, 0x49, 0x46, 0x46]],                                           // RIFF (WEBP container)
        // DOCX / XLSX / ZIP-based Office formats start with PK\x03\x04
        ["application/vnd.openxmlformats-officedocument.wordprocessingml.document"] =
                              [[0x50, 0x4B, 0x03, 0x04]],
    };

    /// <summary>
    /// Returns true if <paramref name="fileHeader"/> matches at least one known signature for
    /// <paramref name="contentType"/>, or if no signature is registered for that type.
    /// </summary>
    public static bool Matches(string contentType, ReadOnlySpan<byte> fileHeader)
    {
        if (!Known.TryGetValue(contentType, out var signatures))
            return true;

        foreach (var sig in signatures)
        {
            if (fileHeader.Length >= sig.Length && fileHeader[..sig.Length].SequenceEqual(sig))
                return true;
        }

        return false;
    }
}
