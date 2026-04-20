namespace DocVault.Application.Abstractions.Text;

/// <summary>A contiguous slice of extracted document text with its character offsets into the original string.</summary>
public sealed record TextChunk(int Index, string Text, int StartChar, int EndChar);
