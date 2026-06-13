using DocVault.Application.Abstractions.Cqrs;
using DocVault.Application.Common.Results;

namespace DocVault.Application.UseCases.Admin.DeadLetterQueue;

public sealed record GetDeadLetterQueueQuery(int Page = 1, int PageSize = 20)
  : IQuery<Result<DeadLetterQueueResult>>;
