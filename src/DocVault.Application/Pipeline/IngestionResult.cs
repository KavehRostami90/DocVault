namespace DocVault.Application.Pipeline;

/// <summary>
/// The output of a completed ingestion pipeline run: extracted plain text and its embedding vector.
/// </summary>
public sealed record IngestionResult(string Text, float[] Embedding);
