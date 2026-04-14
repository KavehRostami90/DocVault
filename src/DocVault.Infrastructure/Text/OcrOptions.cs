namespace DocVault.Infrastructure.Text;

/// <summary>
/// Configuration for the Tesseract OCR engine.
/// </summary>
public sealed class OcrOptions
{
    public const string Section = "Ocr";

    /// <summary>
    /// Absolute path to the directory containing Tesseract <c>.traineddata</c> language files.
    /// On Debian/Ubuntu this is typically <c>/usr/share/tesseract-ocr/5/tessdata</c>.
    /// </summary>
    public string TessDataPath { get; init; } = "/usr/share/tesseract-ocr/5/tessdata";

    /// <summary>
    /// ISO 639-2/T language code(s) to load, e.g. <c>eng</c> or <c>eng+fra</c>.
    /// </summary>
    public string Language { get; init; } = "eng";
}
