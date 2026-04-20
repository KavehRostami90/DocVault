namespace DocVault.Application.Abstractions.Text;

/// <summary>Splits a document's plain text into overlapping chunks suitable for per-chunk embedding.</summary>
public interface ITextChunker
{
    IReadOnlyList<TextChunk> Chunk(string text);
}
