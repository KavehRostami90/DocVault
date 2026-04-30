using DocVault.Infrastructure.Embeddings;
using Xunit;

namespace DocVault.UnitTests.Infrastructure.Embeddings;

public sealed class FakeEmbeddingProviderTests
{
    private readonly FakeEmbeddingProvider _provider = new();

    // -------------------------------------------------------------------------
    // Dimensionality
    // -------------------------------------------------------------------------

    [Fact]
    public async Task EmbedAsync_Returns768DimensionalVector()
    {
        var vector = await _provider.EmbedAsync("hello world");
        Assert.Equal(768, vector.Length);
    }

    [Fact]
    public async Task EmbedAsync_EmptyString_Returns768ZeroVector()
    {
        var vector = await _provider.EmbedAsync(string.Empty);
        Assert.Equal(768, vector.Length);
        Assert.All(vector, v => Assert.Equal(0f, v));
    }

    // -------------------------------------------------------------------------
    // Normalisation
    // -------------------------------------------------------------------------

    [Fact]
    public async Task EmbedAsync_NonEmptyText_VectorIsL2Normalised()
    {
        var vector = await _provider.EmbedAsync("machine learning embeddings");

        var norm = MathF.Sqrt(vector.Sum(v => v * v));
        Assert.Equal(1f, norm, precision: 5);
    }

    [Theory]
    [InlineData("single")]
    [InlineData("two words")]
    [InlineData("the quick brown fox jumps over the lazy dog")]
    public async Task EmbedAsync_AnyNonEmptyText_NormIsApproximatelyOne(string text)
    {
        var vector = await _provider.EmbedAsync(text);

        var norm = MathF.Sqrt(vector.Sum(v => v * v));
        Assert.Equal(1f, norm, precision: 5);
    }

    // -------------------------------------------------------------------------
    // Determinism
    // -------------------------------------------------------------------------

    [Fact]
    public async Task EmbedAsync_SameInputTwice_ReturnsSameVector()
    {
        const string text = "deterministic embedding test";

        var v1 = await _provider.EmbedAsync(text);
        var v2 = await _provider.EmbedAsync(text);

        Assert.Equal(v1, v2);
    }

    // -------------------------------------------------------------------------
    // Distinctness
    // -------------------------------------------------------------------------

    [Fact]
    public async Task EmbedAsync_DifferentTexts_ReturnsDifferentVectors()
    {
        var v1 = await _provider.EmbedAsync("finance quarterly report");
        var v2 = await _provider.EmbedAsync("kubernetes container orchestration");

        // At least one dimension must differ
        Assert.False(v1.SequenceEqual(v2), "Different texts should yield different vectors");
    }

    // -------------------------------------------------------------------------
    // Case insensitivity
    // -------------------------------------------------------------------------

    [Fact]
    public async Task EmbedAsync_SameWordsDifferentCase_ReturnsSameVector()
    {
        var lower = await _provider.EmbedAsync("hello world");
        var upper = await _provider.EmbedAsync("HELLO WORLD");

        Assert.Equal(lower, upper);
    }

    // -------------------------------------------------------------------------
    // Value range
    // -------------------------------------------------------------------------

    [Fact]
    public async Task EmbedAsync_NonEmptyText_AllValuesInNegOneToOne()
    {
        var vector = await _provider.EmbedAsync("some text for range validation");

        Assert.All(vector, v =>
            Assert.True(v >= -1f && v <= 1f, $"Value {v} is outside [-1, 1]"));
    }

    // -------------------------------------------------------------------------
    // Cancellation token accepted (no-op but shouldn't throw)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task EmbedAsync_WithCancellationToken_CompletesNormally()
    {
        using var cts = new CancellationTokenSource();
        var vector = await _provider.EmbedAsync("text", cts.Token);
        Assert.Equal(768, vector.Length);
    }

    // -------------------------------------------------------------------------
    // Batch — EmbedBatchAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task EmbedBatchAsync_ReturnsOneVectorPerInput()
    {
        var texts = new[] { "alpha", "beta", "gamma" };
        var results = await _provider.EmbedBatchAsync(texts);

        Assert.Equal(texts.Length, results.Count);
        Assert.All(results, v => Assert.Equal(768, v.Length));
    }

    [Fact]
    public async Task EmbedBatchAsync_PreservesInputOrder()
    {
        var texts = new[] { "finance quarterly report", "kubernetes container orchestration", "machine learning" };

        var batchResults  = await _provider.EmbedBatchAsync(texts);
        var singleResults = await Task.WhenAll(texts.Select(t => _provider.EmbedAsync(t)));

        for (var i = 0; i < texts.Length; i++)
            Assert.Equal(singleResults[i], batchResults[i]);
    }

    [Fact]
    public async Task EmbedBatchAsync_EmptyList_ReturnsEmptyList()
    {
        var results = await _provider.EmbedBatchAsync([]);
        Assert.Empty(results);
    }

    [Fact]
    public async Task EmbedBatchAsync_SingleText_MatchesEmbedAsync()
    {
        const string text = "single input batch";

        var single = await _provider.EmbedAsync(text);
        var batch  = await _provider.EmbedBatchAsync([text]);

        Assert.Equal(single, batch[0]);
    }
}

