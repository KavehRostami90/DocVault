namespace DocVault.Application.Pipeline.Hooks;

public sealed class PipelineDelegates
{
  public Func<string, float[], CancellationToken, Task>? AfterIndex { get; init; }
}
