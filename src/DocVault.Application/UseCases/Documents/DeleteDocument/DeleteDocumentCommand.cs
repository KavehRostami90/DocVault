using DocVault.Application.Abstractions.Cqrs;
using DocVault.Application.Common.Results;
using DocVault.Domain.Documents;

namespace DocVault.Application.UseCases.Documents.DeleteDocument;

public sealed record DeleteDocumentCommand(DocumentId Id, Guid? CallerId = null, bool IsAdmin = false)
  : ICommand<Result>;
