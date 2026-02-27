namespace DocVault.Application.Pipeline.Stages;

public sealed class IndexStage
{
  public Task IndexAsync(string text, float[] vector, CancellationToken cancellationToken = default)
  {
    // Hook for search index implementations.
    return Task.CompletedTask;
  }
}
