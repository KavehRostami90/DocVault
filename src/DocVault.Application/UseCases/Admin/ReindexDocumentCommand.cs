using DocVault.Application.Abstractions.Cqrs;
using DocVault.Application.Common.Results;
using DocVault.Domain.Documents;

namespace DocVault.Application.UseCases.Admin;

public sealed record ReindexDocumentCommand(DocumentId DocumentId) : ICommand<Result>;
