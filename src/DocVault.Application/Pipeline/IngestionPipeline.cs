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

  public IngestionPipeline(FileReadStage fileRead, TextExtractStage textExtract, EmbeddingStage embedding, IndexStage index, PipelineDelegates hooks)
  {
    _fileRead = fileRead;
    _textExtract = textExtract;
    _embedding = embedding;
    _index = index;
    _hooks = hooks;
  }

  public async Task<string> RunAsync(string path, string contentType, CancellationToken cancellationToken = default)
  {
    var content = await _fileRead.ReadAsync(path, cancellationToken);
    var text = await _textExtract.ExtractAsync(content, contentType, cancellationToken);
    var vector = await _embedding.GenerateAsync(text, cancellationToken);
    await _index.IndexAsync(text, vector, cancellationToken);
    if (_hooks.AfterIndex is not null)
    {
      await _hooks.AfterIndex(text, vector, cancellationToken);
    }
    return text;
  }
}
