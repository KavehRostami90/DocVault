namespace DocVault.Application.Abstractions.Text;

/// <summary>
/// Performs optical character recognition (OCR) on a raw image byte array,
/// returning the recognised plain text.
/// </summary>
public interface IOcrEngine
{
    /// <summary>
    /// Runs OCR on <paramref name="imageBytes"/> and returns the extracted text.
    /// Returns an empty string when no text is found or the image is blank.
    /// </summary>
    Task<string> RecognizeAsync(byte[] imageBytes, CancellationToken cancellationToken = default);
}
