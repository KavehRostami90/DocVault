using DocVault.Application.Pipeline.Stages;

namespace DocVault.Application.Pipeline;

public sealed class IngestionPipeline : IIngestionPipeline
{
  private readonly FileReadStage _fileRead;
  private readonly TextExtractStage _textExtract;
  private readonly ChunkingStage _chunking;
  private readonly EmbeddingStage _embedding;
  private readonly IndexStage _index;

  public IngestionPipeline(
    FileReadStage fileRead,
    TextExtractStage textExtract,
    ChunkingStage chunking,
    EmbeddingStage embedding,
    IndexStage index)
  {
    _fileRead    = fileRead;
    _textExtract = textExtract;
    _chunking    = chunking;
    _embedding   = embedding;
    _index       = index;
  }

  public async Task<IngestionResult> RunAsync(string path, string contentType, CancellationToken cancellationToken = default)
  {
    var content = await _fileRead.ReadAsync(path, cancellationToken);
    var text    = await _textExtract.ExtractAsync(content, contentType, cancellationToken);

    if (string.IsNullOrWhiteSpace(text))
      return new IngestionResult(string.Empty, []);

    var textChunks = _chunking.Chunk(text);
    var texts      = textChunks.Select(c => c.Text).ToList();
    var vectors    = await _embedding.GenerateBatchAsync(texts, cancellationToken);

    var chunkEmbeddings = textChunks
      .Zip(vectors, (chunk, vector) => new ChunkEmbedding(chunk, vector))
      .ToList();

    await _index.IndexAsync(text, chunkEmbeddings[0].Embedding, cancellationToken);

    return new IngestionResult(text, chunkEmbeddings);
  }
}
