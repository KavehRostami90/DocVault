using DocVault.Domain.Documents;

namespace DocVault.Application.UseCases.Search;

public sealed record DocumentSearchSummary(
    DocumentId Id,
    string Title,
    string FileName,
    IReadOnlyList<string> Tags);
