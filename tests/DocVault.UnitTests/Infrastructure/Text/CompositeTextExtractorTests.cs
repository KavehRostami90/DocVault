using DocVault.Application.Abstractions.Text;
using DocVault.Infrastructure.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace DocVault.UnitTests.Infrastructure.Text;

/// <summary>
/// Tests for <see cref="CompositeTextExtractor"/> to verify correct routing of content types to the appropriate text extractor implementations, including delegation to OCR for image types and fallback behaviour for unknown types
/// </summary>
public sealed class CompositeTextExtractorTests
{
    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static CompositeTextExtractor BuildExtractor(string ocrResult = "ocr text")
    {
        var mockEngine = new Mock<IOcrEngine>();
        mockEngine.Setup(e => e.RecognizeAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(ocrResult);
        return new CompositeTextExtractor(
            new ImageOcrExtractor(mockEngine.Object),
            new PdfOcrExtractor(mockEngine.Object, NullLogger<PdfOcrExtractor>.Instance));
    }

    private static MemoryStream TextStream(string text = "hello")
        => new(System.Text.Encoding.UTF8.GetBytes(text));

    // ─── Text / Document types ────────────────────────────────────────────────

    [Theory]
    [InlineData("text/plain")]
    [InlineData("text/csv")]
    [InlineData("application/json")]
    public async Task ExtractAsync_TextTypes_ReturnsPlainText(string contentType)
    {
        var sut = BuildExtractor();
        using var stream = TextStream("hello plain");

        var result = await sut.ExtractAsync(stream, contentType);

        Assert.Equal("hello plain", result);
    }

    // ─── Image types routed to OCR ────────────────────────────────────────────

    [Theory]
    [InlineData("image/png")]
    [InlineData("image/jpeg")]
    [InlineData("image/gif")]
    [InlineData("image/tiff")]
    [InlineData("image/bmp")]
    [InlineData("image/webp")]
    public async Task ExtractAsync_ImageTypes_DelegatesToOcrEngine(string contentType)
    {
        var mockEngine = new Mock<IOcrEngine>();
        mockEngine.Setup(e => e.RecognizeAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync("extracted via ocr");

        var sut = new CompositeTextExtractor(
            new ImageOcrExtractor(mockEngine.Object),
            new PdfOcrExtractor(mockEngine.Object, NullLogger<PdfOcrExtractor>.Instance));

        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        var result = await sut.ExtractAsync(stream, contentType);

        Assert.Equal("extracted via ocr", result);
        mockEngine.Verify(e => e.RecognizeAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("IMAGE/PNG")]
    [InlineData("Image/Jpeg")]
    [InlineData("IMAGE/WEBP")]
    public async Task ExtractAsync_ImageTypes_ContentTypeCaseInsensitive(string contentType)
    {
        var mockEngine = new Mock<IOcrEngine>();
        mockEngine.Setup(e => e.RecognizeAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync("ocr result");

        var sut = new CompositeTextExtractor(
            new ImageOcrExtractor(mockEngine.Object),
            new PdfOcrExtractor(mockEngine.Object, NullLogger<PdfOcrExtractor>.Instance));

        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        var result = await sut.ExtractAsync(stream, contentType);

        Assert.Equal("ocr result", result);
    }

    // ─── Unknown type falls back to plain text ────────────────────────────────

    [Fact]
    public async Task ExtractAsync_UnknownType_FallsBackToPlainTextExtractor()
    {
        var sut = BuildExtractor();
        using var stream = TextStream("fallback content");

        var result = await sut.ExtractAsync(stream, "application/octet-stream");

        Assert.Equal("fallback content", result);
    }

    // ─── Null / whitespace content-type ──────────────────────────────────────

    [Fact]
    public async Task ExtractAsync_NullContentType_FallsBackGracefully()
    {
        var sut = BuildExtractor();
        using var stream = TextStream("content");

        var result = await sut.ExtractAsync(stream, null!);

        Assert.Equal("content", result);
    }
}
