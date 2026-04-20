namespace DocVault.Api.Contracts.Search;

/// <summary>
/// Single search result entry.
/// </summary>
/// <param name="Id">Unique identifier of the matching document.</param>
/// <param name="Title">Title of the matching document.</param>
/// <param name="Snippet">Context-relevant text snippet from the best matching chunk, or the start of the document text.</param>
/// <param name="Score">Relevance score (cosine similarity for semantic/hybrid; term-frequency proxy for keyword).</param>
public sealed record SearchResultItemResponse(Guid Id, string Title, string Snippet, double Score);
