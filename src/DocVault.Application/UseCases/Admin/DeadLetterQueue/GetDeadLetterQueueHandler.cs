using DocVault.Application.Abstractions.Cqrs;
using DocVault.Application.Abstractions.Persistence;
using DocVault.Application.Common.Results;

namespace DocVault.Application.UseCases.Admin.DeadLetterQueue;

public sealed class GetDeadLetterQueueHandler
  : IQueryHandler<GetDeadLetterQueueQuery, Result<DeadLetterQueueResult>>
{
  private readonly IFailedIndexingJobRepository _repo;

  public GetDeadLetterQueueHandler(IFailedIndexingJobRepository repo) => _repo = repo;

  public async Task<Result<DeadLetterQueueResult>> HandleAsync(
    GetDeadLetterQueueQuery query,
    CancellationToken cancellationToken = default)
  {
    var total   = await _repo.CountAsync(cancellationToken);
    var entries = await _repo.GetAllAsync(query.Page, query.PageSize, cancellationToken);

    var dtos = entries.Select(e => new FailedIndexingJobDto(
      e.Id, e.JobId, e.StoragePath, e.ContentType,
      e.AttemptCount, e.MaxAttempts, e.LastError,
      e.FirstFailedAt, e.LastFailedAt, e.NextRetryAt, e.IsExhausted)).ToList();

    return Result<DeadLetterQueueResult>.Success(new DeadLetterQueueResult(dtos, total));
  }
}

public sealed record DeadLetterQueueResult(IReadOnlyList<FailedIndexingJobDto> Items, int Total);
