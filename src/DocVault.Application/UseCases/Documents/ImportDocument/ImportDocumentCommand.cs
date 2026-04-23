using DocVault.Application.Abstractions.Cqrs;
using DocVault.Application.Common.Results;
using DocVault.Domain.Documents;

namespace DocVault.Application.UseCases.Documents.ImportDocument;

/// <summary>All metadata is sourced from the uploaded file; nothing is trusted from the client except Title and Tags.</summary>
public sealed record ImportDocumentCommand(
  string Title,
  string FileName,
  string ContentType,
  long Size,
  IReadOnlyCollection<string> Tags,
  Stream Content,
  Guid? OwnerId = null) : ICommand<Result<DocumentId>>;
