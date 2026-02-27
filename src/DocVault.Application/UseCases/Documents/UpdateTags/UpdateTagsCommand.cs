using DocVault.Domain.Documents;

namespace DocVault.Application.UseCases.Documents.UpdateTags;

public sealed record UpdateTagsCommand(DocumentId Id, IReadOnlyCollection<string> Tags);
