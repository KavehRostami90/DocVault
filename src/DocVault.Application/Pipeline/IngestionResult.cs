namespace DocVault.Application.Pipeline;

/// <summary>
/// The output of a completed ingestion pipeline run: extracted plain text and its embedding vector.
/// <para>
/// <see cref="Embedding"/> is <c>null</c> when no text was extracted (e.g. OCR produced no output).
/// Consumers must check for <c>null</c> before using the embedding.
/// </para>
/// </summary>
public sealed record IngestionResult(string Text, float[]? Embedding);
