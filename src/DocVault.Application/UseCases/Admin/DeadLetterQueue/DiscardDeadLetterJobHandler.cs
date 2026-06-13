using DocVault.Application.Abstractions.Cqrs;
using DocVault.Application.Abstractions.Messaging;
using DocVault.Application.Abstractions.Persistence;
using DocVault.Application.Common.Results;

namespace DocVault.Application.UseCases.Admin.DeadLetterQueue;

public sealed record DiscardDeadLetterJobCommand(Guid EntryId) : ICommand<Result>;

public sealed class DiscardDeadLetterJobHandler : ICommandHandler<DiscardDeadLetterJobCommand, Result>
{
  private readonly IFailedIndexingJobRepository _dlqRepo;
  private readonly IImportJobRepository         _importJobRepo;
  private readonly IDocumentRepository          _documentRepo;
  private readonly IUnitOfWork                  _unitOfWork;
  private readonly IDomainEventDispatcher       _eventDispatcher;

  public DiscardDeadLetterJobHandler(
    IFailedIndexingJobRepository dlqRepo,
    IImportJobRepository importJobRepo,
    IDocumentRepository documentRepo,
    IUnitOfWork unitOfWork,
    IDomainEventDispatcher eventDispatcher)
  {
    _dlqRepo         = dlqRepo;
    _importJobRepo   = importJobRepo;
    _documentRepo    = documentRepo;
    _unitOfWork      = unitOfWork;
    _eventDispatcher = eventDispatcher;
  }

  public async Task<Result> HandleAsync(DiscardDeadLetterJobCommand command, CancellationToken cancellationToken = default)
  {
    var entry = await _dlqRepo.GetByIdAsync(command.EntryId, cancellationToken);
    if (entry is null)
      return Result.Failure("Dead-letter entry not found.");

    var job = await _importJobRepo.GetAsync(entry.JobId, cancellationToken);

    entry.Discard();
    await _dlqRepo.UpdateAsync(entry, cancellationToken);

    if (job is not null)
    {
      job.MarkFailed("Discarded by admin.");
      await _importJobRepo.UpdateAsync(job, cancellationToken);

      var document = await _documentRepo.GetAsync(job.DocumentId, cancellationToken);
      if (document is not null)
      {
        document.MarkFailed("Discarded by admin.");
        await _documentRepo.UpdateAsync(document, cancellationToken);
      }

      await _unitOfWork.SaveChangesAsync(cancellationToken);

      var doc2 = await _documentRepo.GetAsync(job.DocumentId, cancellationToken);
      if (doc2 is not null)
      {
        await _eventDispatcher.DispatchAsync(doc2.DomainEvents, cancellationToken);
        doc2.ClearDomainEvents();
      }
    }
    else
    {
      await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    return Result.Success();
  }
}
