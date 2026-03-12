using DocVault.Application.Abstractions.Embeddings;

namespace DocVault.Infrastructure.Embeddings;

/// <summary>
/// Development embedding provider that uses <b>feature hashing</b> (the hashing trick)
/// to produce a deterministic, fixed-size 128-dimensional bag-of-words vector.
/// <para>
/// Each whitespace-delimited token is lowercased, hashed with a stable 32-bit algorithm,
/// and its count is accumulated into the corresponding bucket. The final vector is
/// L2-normalised so that cosine similarity comparisons return sensible results.
/// This is a real, if simple, NLP technique — not semantically deep but consistent
/// and usable for local development and testing.
/// </para>
/// <para>
/// Replace with <c>OpenAI text-embedding-ada-002</c>, Azure OpenAI, or a local ONNX model
/// for production-quality semantic search.
/// </para>
/// </summary>
public sealed class FakeEmbeddingProvider : IEmbeddingProvider
{
  private const int Dimensions = 128;

  /// <summary>
  /// Produces a 128-dimensional feature-hashed embedding of <paramref name="text"/>.
  /// </summary>
  /// <param name="text">Input text to embed.</param>
  /// <param name="cancellationToken">Cancellation token (unused — computation is synchronous).</param>
  /// <returns>An L2-normalised float array of length 128.</returns>
  public Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default)
  {
    var vector = new float[Dimensions];

    foreach (var token in text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
    {
      var bucket = Math.Abs(StableHash(token.ToLowerInvariant())) % Dimensions;
      vector[bucket] += 1f;
    }

    // L2 normalise so cosine similarity == dot product
    var norm = MathF.Sqrt(vector.Sum(v => v * v));
    if (norm > 0f)
      for (var i = 0; i < Dimensions; i++)
        vector[i] /= norm;

    return Task.FromResult(vector);
  }

  /// <summary>
  /// FNV-1a 32-bit hash — fast, stable across runs, and requires no dependencies.
  /// </summary>
  private static int StableHash(string s)
  {
    unchecked
    {
      var hash = (int)2166136261u;
      foreach (var c in s)
        hash = (hash ^ c) * 16777619;
      return hash;
    }
  }
}
