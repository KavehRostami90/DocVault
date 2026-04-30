using System.Security.Cryptography;
using System.Text;
using DocVault.Application.Abstractions.Embeddings;
using DocVault.Infrastructure.Embeddings;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace DocVault.UnitTests.Infrastructure.Embeddings;

public sealed class CachingEmbeddingProviderTests
{
  // ── helpers ──────────────────────────────────────────────────────────────────

  private const string ModelTag = "test-model";

  /// <summary>Mirrors the key algorithm in <see cref="CachingEmbeddingProvider"/>.</summary>
  private static string Key(string text)
  {
    var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{ModelTag}|{text}"));
    return Convert.ToHexStringLower(hash);
  }

  private static float[] Vec(float seed) => [seed, seed + 1f, seed + 2f];

  private static CachingEmbeddingProvider Build(
    IEmbeddingProvider inner,
    IEmbeddingCache cache,
    bool enabled = true) =>
    new(inner,
        cache,
        ModelTag,
        Options.Create(new EmbeddingCacheOptions { IsEnabled = enabled }),
        NullLogger<CachingEmbeddingProvider>.Instance);

  // ── tests ────────────────────────────────────────────────────────────────────

  [Fact]
  public async Task EmbedBatchAsync_AllMiss_ForwardsAllTextsToInner()
  {
    var texts   = new[] { "alpha", "beta", "gamma" };
    var vectors = texts.Select((_, i) => Vec(i)).ToArray();

    var inner = new Mock<IEmbeddingProvider>();
    // The decorator creates a new List<string> for the misses, so match by sequence equality.
    inner.Setup(p => p.EmbedBatchAsync(
           It.Is<IReadOnlyList<string>>(l => l.SequenceEqual(texts)), default))
         .ReturnsAsync(vectors);

    var cache = new Mock<IEmbeddingCache>();
    cache.Setup(c => c.GetManyAsync(It.IsAny<IEnumerable<string>>(), default))
         .ReturnsAsync(new Dictionary<string, float[]>());
    cache.Setup(c => c.SetManyAsync(It.IsAny<IReadOnlyDictionary<string, float[]>>(), default))
         .Returns(Task.CompletedTask);

    var sut    = Build(inner.Object, cache.Object);
    var result = await sut.EmbedBatchAsync(texts);

    Assert.Equal(3, result.Count);
    for (var i = 0; i < texts.Length; i++)
      Assert.Equal(vectors[i], result[i]);

    inner.Verify(p => p.EmbedBatchAsync(It.IsAny<IReadOnlyList<string>>(), default), Times.Once);
    cache.Verify(c => c.SetManyAsync(
      It.Is<IReadOnlyDictionary<string, float[]>>(d => d.Count == 3), default), Times.Once);
  }

  [Fact]
  public async Task EmbedBatchAsync_AllHit_NeverCallsInner()
  {
    var texts   = new[] { "alpha", "beta" };
    var hitDict = new Dictionary<string, float[]>
    {
      [Key("alpha")] = Vec(0),
      [Key("beta")]  = Vec(1),
    };

    var inner = new Mock<IEmbeddingProvider>();
    var cache = new Mock<IEmbeddingCache>();
    cache.Setup(c => c.GetManyAsync(It.IsAny<IEnumerable<string>>(), default))
         .ReturnsAsync(hitDict);

    var sut    = Build(inner.Object, cache.Object);
    var result = await sut.EmbedBatchAsync(texts);

    Assert.Equal(Vec(0), result[0]);
    Assert.Equal(Vec(1), result[1]);

    inner.Verify(p => p.EmbedBatchAsync(It.IsAny<IReadOnlyList<string>>(), default), Times.Never);
    cache.Verify(c => c.SetManyAsync(It.IsAny<IReadOnlyDictionary<string, float[]>>(), default), Times.Never);
  }

  [Fact]
  public async Task EmbedBatchAsync_PartialHit_OnlyMissesForwardedAndOrderPreserved()
  {
    // "alpha" and "gamma" are cached; "beta" is a miss.
    var texts = new[] { "alpha", "beta", "gamma" };

    var cachedEntries = new Dictionary<string, float[]>
    {
      [Key("alpha")] = Vec(0),
      [Key("gamma")] = Vec(2),
    };
    var missVector = Vec(1);

    var inner = new Mock<IEmbeddingProvider>();
    inner.Setup(p => p.EmbedBatchAsync(
           It.Is<IReadOnlyList<string>>(l => l.Count == 1 && l[0] == "beta"), default))
         .ReturnsAsync(new float[][] { missVector });

    var cache = new Mock<IEmbeddingCache>();
    cache.Setup(c => c.GetManyAsync(It.IsAny<IEnumerable<string>>(), default))
         .ReturnsAsync(cachedEntries);
    cache.Setup(c => c.SetManyAsync(It.IsAny<IReadOnlyDictionary<string, float[]>>(), default))
         .Returns(Task.CompletedTask);

    var sut    = Build(inner.Object, cache.Object);
    var result = await sut.EmbedBatchAsync(texts);

    // Original order must be preserved: alpha, beta, gamma.
    Assert.Equal(Vec(0), result[0]);
    Assert.Equal(Vec(1), result[1]);
    Assert.Equal(Vec(2), result[2]);

    // Inner was called with exactly the one miss.
    inner.Verify(p => p.EmbedBatchAsync(
      It.Is<IReadOnlyList<string>>(l => l.Count == 1 && l[0] == "beta"), default), Times.Once);

    // Only the miss was persisted in the cache.
    cache.Verify(c => c.SetManyAsync(
      It.Is<IReadOnlyDictionary<string, float[]>>(d => d.Count == 1 && d.ContainsKey(Key("beta"))),
      default), Times.Once);
  }

  [Fact]
  public async Task EmbedBatchAsync_Disabled_AlwaysForwardsToInnerWithoutTouchingCache()
  {
    // When disabled the original IReadOnlyList<string> reference is passed directly through.
    var texts   = new[] { "alpha", "beta" };
    var vectors = new float[][] { Vec(0), Vec(1) };

    var inner = new Mock<IEmbeddingProvider>();
    inner.Setup(p => p.EmbedBatchAsync(texts, default))
         .ReturnsAsync(vectors);

    var cache = new Mock<IEmbeddingCache>();

    var sut    = Build(inner.Object, cache.Object, enabled: false);
    var result = await sut.EmbedBatchAsync(texts);

    Assert.Equal(2, result.Count);
    inner.Verify(p => p.EmbedBatchAsync(texts, default), Times.Once);
    cache.Verify(c => c.GetManyAsync(It.IsAny<IEnumerable<string>>(), default), Times.Never);
    cache.Verify(c => c.SetManyAsync(It.IsAny<IReadOnlyDictionary<string, float[]>>(), default), Times.Never);
  }

  [Fact]
  public async Task EmbedBatchAsync_EmptyBatch_ReturnsEmptyWithoutCallingAnything()
  {
    var inner = new Mock<IEmbeddingProvider>();
    var cache = new Mock<IEmbeddingCache>();

    var sut    = Build(inner.Object, cache.Object);
    var result = await sut.EmbedBatchAsync([]);

    Assert.Empty(result);
    inner.Verify(p => p.EmbedBatchAsync(It.IsAny<IReadOnlyList<string>>(), default), Times.Never);
    cache.Verify(c => c.GetManyAsync(It.IsAny<IEnumerable<string>>(), default), Times.Never);
  }

  [Fact]
  public async Task EmbedAsync_SingleText_ReturnsCorrectVector()
  {
    var vector = Vec(42);

    var inner = new Mock<IEmbeddingProvider>();
    inner.Setup(p => p.EmbedBatchAsync(
           It.Is<IReadOnlyList<string>>(l => l.Count == 1 && l[0] == "hello"), default))
         .ReturnsAsync(new float[][] { vector });

    var cache = new Mock<IEmbeddingCache>();
    cache.Setup(c => c.GetManyAsync(It.IsAny<IEnumerable<string>>(), default))
         .ReturnsAsync(new Dictionary<string, float[]>());
    cache.Setup(c => c.SetManyAsync(It.IsAny<IReadOnlyDictionary<string, float[]>>(), default))
         .Returns(Task.CompletedTask);

    var sut    = Build(inner.Object, cache.Object);
    var result = await sut.EmbedAsync("hello");

    Assert.Equal(vector, result);
  }
}
