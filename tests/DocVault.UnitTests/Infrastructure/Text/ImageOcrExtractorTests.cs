using DocVault.Application.Abstractions.Text;
using DocVault.Infrastructure.Text;
using Moq;
using Xunit;

namespace DocVault.UnitTests.Infrastructure.Text;

public sealed class ImageOcrExtractorTests
{
    private readonly Mock<IOcrEngine> _ocrEngine = new();
    private readonly ImageOcrExtractor _extractor;

    public ImageOcrExtractorTests()
        => _extractor = new ImageOcrExtractor(_ocrEngine.Object);

    // -------------------------------------------------------------------------
    // Delegation to OCR engine
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ExtractAsync_CallsOcrEngineWithImageBytes()
    {
        var bytes = new byte[] { 0x89, 0x50, 0x4E, 0x47 }; // PNG header bytes
        _ocrEngine.Setup(e => e.RecognizeAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync("Hello World");

        using var stream = new MemoryStream(bytes);
        var result = await _extractor.ExtractAsync(stream, "image/png");

        Assert.Equal("Hello World", result);
        _ocrEngine.Verify(e => e.RecognizeAsync(
            It.Is<byte[]>(b => b.SequenceEqual(bytes)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExtractAsync_PassesCancellationTokenToEngine()
    {
        using var cts = new CancellationTokenSource();
        _ocrEngine.Setup(e => e.RecognizeAsync(It.IsAny<byte[]>(), cts.Token))
                  .ReturnsAsync(string.Empty);

        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        await _extractor.ExtractAsync(stream, "image/jpeg", cts.Token);

        _ocrEngine.Verify(e => e.RecognizeAsync(It.IsAny<byte[]>(), cts.Token), Times.Once);
    }

    // -------------------------------------------------------------------------
    // Stream rewinding
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ExtractAsync_ResetsSeekableStreamToPositionZero()
    {
        _ocrEngine.Setup(e => e.RecognizeAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(string.Empty);

        using var stream = new MemoryStream(new byte[] { 1, 2, 3, 4 });
        await _extractor.ExtractAsync(stream, "image/png");

        Assert.Equal(0, stream.Position);
    }

    // -------------------------------------------------------------------------
    // Empty / zero-byte images
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ExtractAsync_EmptyStream_ReturnsEmptyString()
    {
        _ocrEngine.Setup(e => e.RecognizeAsync(It.Is<byte[]>(b => b.Length == 0), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(string.Empty);

        using var stream = new MemoryStream();
        var result = await _extractor.ExtractAsync(stream, "image/png");

        Assert.Equal(string.Empty, result);
    }

    // -------------------------------------------------------------------------
    // Engine returns trimmed text
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("  Hello  ", "  Hello  ")]
    [InlineData("", "")]
    [InlineData("Invoice #1234\nTotal: $99.99", "Invoice #1234\nTotal: $99.99")]
    public async Task ExtractAsync_ReturnsExactlyWhatEngineReturns(string engineOutput, string expected)
    {
        _ocrEngine.Setup(e => e.RecognizeAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(engineOutput);

        using var stream = new MemoryStream(new byte[] { 0x01 });
        var result = await _extractor.ExtractAsync(stream, "image/png");

        Assert.Equal(expected, result);
    }
}
