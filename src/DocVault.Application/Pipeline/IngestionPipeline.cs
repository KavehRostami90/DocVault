using DocVault.Application.Pipeline.Hooks;
using DocVault.Application.Pipeline.Stages;

namespace DocVault.Application.Pipeline;

public sealed class IngestionPipeline : IIngestionPipeline
{
  private readonly FileReadStage _fileRead;
  private readonly TextExtractStage _textExtract;
  private readonly ChunkingStage _chunking;
  private readonly EmbeddingStage _embedding;
  private readonly IndexStage _index;
  private readonly PipelineDelegates _hooks;

  public IngestionPipeline(
    FileReadStage fileRead,
    TextExtractStage textExtract,
    ChunkingStage chunking,
    EmbeddingStage embedding,
    IndexStage index,
    PipelineDelegates hooks)
  {
    _fileRead = fileRead;
    _textExtract = textExtract;
    _chunking = chunking;
    _embedding = embedding;
    _index = index;
    _hooks = hooks;
  }

  public async Task<IngestionResult> RunAsync(string path, string contentType, CancellationToken cancellationToken = default)
  {
    var content = await _fileRead.ReadAsync(path, cancellationToken);
    var text = await _textExtract.ExtractAsync(content, contentType, cancellationToken);

    // No text extracted (e.g. OCR failed or blank image) — skip chunking and embedding.
    if (string.IsNullOrWhiteSpace(text))
      return new IngestionResult(string.Empty, []);

    var textChunks = _chunking.Chunk(text);
    var chunkEmbeddings = new List<ChunkEmbedding>(textChunks.Count);
    foreach (var chunk in textChunks)
    {
      var vector = await _embedding.GenerateAsync(chunk.Text, cancellationToken);
      chunkEmbeddings.Add(new ChunkEmbedding(chunk, vector));
    }

    // Preserve hook contract: pass full text and the first chunk's vector.
    await _index.IndexAsync(text, chunkEmbeddings[0].Embedding, cancellationToken);
    if (_hooks.AfterIndex is not null)
      await _hooks.AfterIndex(text, chunkEmbeddings[0].Embedding, cancellationToken);

    return new IngestionResult(text, chunkEmbeddings);
  }
}
