namespace DocVault.Domain.Documents;

public sealed class DocumentChunk
{
    public Guid Id { get; private init; } = Guid.NewGuid();
    public DocumentId DocumentId { get; private init; }
    public int ChunkIndex { get; private init; }
    public string Text { get; private init; }
    public float[]? Embedding { get; private set; }
    public int StartChar { get; private init; }
    public int EndChar { get; private init; }

    private DocumentChunk()
    {
        Text = string.Empty;
    }

    public static DocumentChunk Create(DocumentId documentId, int index, string text, int startChar, int endChar)
        => new()
        {
            DocumentId = documentId,
            ChunkIndex = index,
            Text = text,
            StartChar = startChar,
            EndChar = endChar
        };

    public void AttachEmbedding(float[] embedding) => Embedding = embedding;
}
