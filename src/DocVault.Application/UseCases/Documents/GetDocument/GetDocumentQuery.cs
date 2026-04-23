using DocVault.Application.Abstractions.Cqrs;
using DocVault.Application.Common.Results;
using DocVault.Domain.Documents;

namespace DocVault.Application.UseCases.Documents.GetDocument;

public sealed record GetDocumentQuery(DocumentId Id, Guid? CallerId = null, bool IsAdmin = false)
  : IQuery<Result<Document>>;
