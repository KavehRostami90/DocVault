using DocVault.Application.Abstractions.Text;

namespace DocVault.Application.Pipeline.Stages;

public sealed class ChunkingStage
{
    private readonly ITextChunker _chunker;

    public ChunkingStage(ITextChunker chunker) => _chunker = chunker;

    public IReadOnlyList<TextChunk> Chunk(string text) => _chunker.Chunk(text);
}
