using DocVault.Application.Abstractions.Cqrs;
using DocVault.Application.Common.Results;

namespace DocVault.Application.UseCases.Qa;

public sealed record AskQuestionQuery(
  string Question,
  int MaxDocuments = 8,
  int MaxContexts = 6,
  Guid? DocumentId = null,
  Guid? OwnerId = null,
  bool IsAdmin = false) : IQuery<Result<AskQuestionResult>>;
