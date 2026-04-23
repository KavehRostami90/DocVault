using DocVault.Application.Abstractions.Cqrs;
using DocVault.Application.Common.Results;
using DocVault.Domain.Documents;

namespace DocVault.Application.UseCases.Imports.StartImportJob;

public sealed record StartImportJobCommand(
  DocumentId DocumentId,
  string FileName,
  string StoragePath,
  string ContentType,
  Guid? CallerId = null,
  bool IsAdmin = false) : ICommand<Result<Guid>>;
