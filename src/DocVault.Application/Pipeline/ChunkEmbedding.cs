using DocVault.Application.Abstractions.Text;

namespace DocVault.Application.Pipeline;

/// <summary>Pairs a text chunk with its embedding vector produced during ingestion.</summary>
public sealed record ChunkEmbedding(TextChunk Chunk, float[] Embedding);
