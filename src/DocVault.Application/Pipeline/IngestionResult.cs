namespace DocVault.Application.Pipeline;

/// <summary>
/// The output of a completed ingestion pipeline run: extracted plain text and the per-chunk embeddings.
/// <para>
/// <see cref="Chunks"/> is empty when no text was extracted (e.g. OCR produced no output).
/// </para>
/// </summary>
public sealed record IngestionResult(string Text, IReadOnlyList<ChunkEmbedding> Chunks);
