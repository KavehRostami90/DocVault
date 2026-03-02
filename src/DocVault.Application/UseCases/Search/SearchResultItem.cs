using DocVault.Domain.Documents;

namespace DocVault.Application.UseCases.Search;

/// <summary>Application-layer search result — pairs a matched document with its relevance score.</summary>
public sealed record SearchResultItem(Document Document, double Score);
