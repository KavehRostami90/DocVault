using DocVault.Application.Abstractions.Cqrs;
using DocVault.Application.Common.Results;

namespace DocVault.Application.UseCases.Admin.DeadLetterQueue;

public sealed record RetryDeadLetterJobCommand(Guid EntryId) : ICommand<Result>;
