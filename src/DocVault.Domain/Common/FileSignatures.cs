using System.Text;

namespace DocVault.Domain.Common;

/// <summary>
/// Validates uploaded file headers against known magic-byte signatures.
/// Text-based types (plain, markdown, JSON) have no reliable magic bytes and are accepted as-is.
/// </summary>
public static class FileSignatures
{
    // Minimum header bytes needed to run all validators (WebP needs 12, DOCX needs ~59).
    public const int RequiredHeaderBytes = 64;

    private static readonly Dictionary<string, Func<ReadOnlySpan<byte>, bool>> Validators =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["application/pdf"] = h => StartsWith(h, 0x25, 0x50, 0x44, 0x46),               // %PDF
            ["image/png"]       = h => StartsWith(h, 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A),
            ["image/jpeg"]      = h => StartsWith(h, 0xFF, 0xD8, 0xFF),
            ["image/gif"]       = h => StartsWith(h, 0x47, 0x49, 0x46, 0x38, 0x37, 0x61)    // GIF87a
                                    || StartsWith(h, 0x47, 0x49, 0x46, 0x38, 0x39, 0x61),   // GIF89a
            ["image/bmp"]       = h => StartsWith(h, 0x42, 0x4D),
            ["image/tiff"]      = h => StartsWith(h, 0x49, 0x49, 0x2A, 0x00)                // II* little-endian
                                    || StartsWith(h, 0x4D, 0x4D, 0x00, 0x2A),               // MM* big-endian

            // WebP: RIFF container requires "WEBP" at bytes 8-11 to distinguish from WAV/AVI.
            ["image/webp"] = h => h.Length >= 12
                               && StartsWith(h, 0x52, 0x49, 0x46, 0x46)                     // RIFF
                               && h[8..12].SequenceEqual(new byte[] { 0x57, 0x45, 0x42, 0x50 }), // WEBP

            // DOCX: ZIP container requires at least one OOXML entry name to distinguish from plain ZIPs.
            ["application/vnd.openxmlformats-officedocument.wordprocessingml.document"] = IsDocx,
        };

    /// <summary>
    /// Returns true if <paramref name="fileHeader"/> passes the signature check for
    /// <paramref name="contentType"/>, or if no check is registered for that type.
    /// </summary>
    public static bool Matches(string contentType, ReadOnlySpan<byte> fileHeader)
    {
        return !Validators.TryGetValue(contentType, out var validate) || validate(fileHeader);
    }

    // ZIP local-file-header layout:
    //   [0-3]   PK\x03\x04
    //   [26-27] file name length (uint16 LE)
    //   [28-29] extra field length (uint16 LE)
    //   [30..]  file name bytes
    // OOXML packages always start with [Content_Types].xml, _rels/.rels, or word/* entries.
    private static bool IsDocx(ReadOnlySpan<byte> h)
    {
        if (!StartsWith(h, 0x50, 0x4B, 0x03, 0x04)) return false;  // must be ZIP
        if (h.Length < 30) return false;

        var fileNameLength = h[26] | (h[27] << 8);
        if (fileNameLength <= 0 || h.Length < 30 + fileNameLength) return false;

        var name = Encoding.ASCII.GetString(h.Slice(30, fileNameLength));
        return name.StartsWith("[Content_Types]",  StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("word/",            StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("_rels/",           StringComparison.OrdinalIgnoreCase);
    }

    private static bool StartsWith(ReadOnlySpan<byte> header, params byte[] signature)
        => header.Length >= signature.Length && header[..signature.Length].SequenceEqual(signature);
}
