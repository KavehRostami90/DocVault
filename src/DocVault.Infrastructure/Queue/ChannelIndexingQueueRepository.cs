using DocVault.Application.Abstractions.Persistence;
using DocVault.Application.Background.Queue;

namespace DocVault.Infrastructure.Queue;

/// <summary>
/// In-memory implementation of <see cref="IIndexingQueueRepository"/> that delegates
/// to the in-process <see cref="IWorkQueue{T}"/>. Used when no persistent database is
/// configured (dev / unit tests) — the channel IS the queue, so no DB row is written.
/// </summary>
public sealed class ChannelIndexingQueueRepository : IIndexingQueueRepository
{
  private readonly IWorkQueue<IndexingWorkItem> _queue;

  public ChannelIndexingQueueRepository(IWorkQueue<IndexingWorkItem> queue) => _queue = queue;

  public Task AddAsync(IndexingWorkItem item, CancellationToken cancellationToken = default)
  {
    _queue.Enqueue(item);
    return Task.CompletedTask;
  }
}
