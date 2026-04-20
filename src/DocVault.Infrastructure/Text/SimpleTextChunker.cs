using DocVault.Application.Abstractions.Text;

namespace DocVault.Infrastructure.Text;

/// <summary>
/// Splits text into overlapping fixed-size word windows.
/// Each chunk records its exact character offsets into the original string so callers
/// can highlight or retrieve the matched passage without re-parsing.
/// </summary>
public sealed class SimpleTextChunker : ITextChunker
{
    private readonly int _chunkSize;  // words per chunk
    private readonly int _overlap;    // words shared between adjacent chunks

    public SimpleTextChunker(int chunkSize = 400, int overlap = 80)
    {
        if (chunkSize < 1) throw new ArgumentOutOfRangeException(nameof(chunkSize));
        if (overlap < 0 || overlap >= chunkSize) throw new ArgumentOutOfRangeException(nameof(overlap));
        _chunkSize = chunkSize;
        _overlap = overlap;
    }

    public IReadOnlyList<TextChunk> Chunk(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return [];

        // Scan words and record their start offsets in the original string.
        var words = new List<(int Start, int End)>();
        var i = 0;
        while (i < text.Length)
        {
            while (i < text.Length && char.IsWhiteSpace(text[i]))
                i++;
            if (i >= text.Length)
                break;

            var wordStart = i;
            while (i < text.Length && !char.IsWhiteSpace(text[i]))
                i++;

            words.Add((wordStart, i));
        }

        if (words.Count == 0)
            return [];

        var chunks = new List<TextChunk>();
        var step = _chunkSize - _overlap;
        var chunkIndex = 0;

        for (var start = 0; start < words.Count; start += step)
        {
            var end = Math.Min(start + _chunkSize, words.Count);
            var startChar = words[start].Start;
            var endChar = words[end - 1].End;
            chunks.Add(new TextChunk(chunkIndex++, text[startChar..endChar], startChar, endChar));

            if (end == words.Count)
                break;
        }

        return chunks;
    }
}
