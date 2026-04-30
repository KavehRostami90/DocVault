namespace DocVault.Api.Contracts.Search;

/// <summary>Single search result entry returned by the search endpoint.</summary>
/// <param name="Id">Unique identifier of the matching document.</param>
/// <param name="Title">Title of the matching document.</param>
/// <param name="FileName">Original file name of the document.</param>
/// <param name="Tags">Tags associated with the document.</param>
/// <param name="Snippet">Text snippet from the best matching chunk (max 120 chars, HTML stripped).</param>
/// <param name="Score">Relevance score (cosine similarity for semantic/hybrid; ts_rank proxy for keyword).</param>
public sealed record SearchResultItemResponse(
    Guid Id,
    string Title,
    string FileName,
    IReadOnlyList<string> Tags,
    string Snippet,
    double Score);
