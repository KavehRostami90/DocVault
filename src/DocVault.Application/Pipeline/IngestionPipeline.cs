using DocVault.Application.Pipeline.Stages;
using DocVault.Application.Pipeline.Hooks;

namespace DocVault.Application.Pipeline;

public sealed class IngestionPipeline : IIngestionPipeline
{
  private readonly FileReadStage _fileRead;
  private readonly TextExtractStage _textExtract;
  private readonly EmbeddingStage _embedding;
  private readonly IndexStage _index;
  private readonly PipelineDelegates _hooks;

  public IngestionPipeline(FileReadStage fileRead,
    TextExtractStage textExtract,
    EmbeddingStage embedding,
    IndexStage index, PipelineDelegates hooks)
  {
    _fileRead = fileRead;
    _textExtract = textExtract;
    _embedding = embedding;
    _index = index;
    _hooks = hooks;
  }

  public async Task<IngestionResult> RunAsync(string path, string contentType, CancellationToken cancellationToken = default)
  {
    var content = await _fileRead.ReadAsync(path, cancellationToken);
    var text = await _textExtract.ExtractAsync(content, contentType, cancellationToken);

    // No text extracted (e.g. OCR failed or blank image) — skip embedding and indexing.
    // Embedding an empty string violates the IEmbeddingProvider contract and would store
    // a meaningless zero-vector that can produce false-positive search matches.
    if (string.IsNullOrWhiteSpace(text))
      return new IngestionResult(string.Empty, null);

    var vector = await _embedding.GenerateAsync(text, cancellationToken);
    await _index.IndexAsync(text, vector, cancellationToken);
    if (_hooks.AfterIndex is not null)
      await _hooks.AfterIndex(text, vector, cancellationToken);
    return new IngestionResult(text, vector);
  }
}
