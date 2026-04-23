using DocVault.Application.Abstractions.Cqrs;
using DocVault.Application.Common.Results;
using DocVault.Domain.Imports;

namespace DocVault.Application.UseCases.Imports.GetImportStatus;

public sealed record GetImportStatusQuery(Guid Id, Guid? CallerId = null, bool IsAdmin = false)
  : IQuery<Result<ImportJob>>;
