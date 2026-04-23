using DocVault.Application.Abstractions.Cqrs;
using DocVault.Application.Common.Results;
using DocVault.Domain.Documents;

namespace DocVault.Application.UseCases.Documents.GetDocumentFile;

public sealed record GetDocumentFileQuery(DocumentId Id, Guid? CallerId = null, bool IsAdmin = false)
  : IQuery<Result<DocumentFileReference>>;
