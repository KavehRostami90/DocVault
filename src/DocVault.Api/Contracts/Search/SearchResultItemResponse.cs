namespace DocVault.Api.Contracts.Search;

/// <summary>
/// Single search result entry.
/// </summary>
/// <param name="Id">Unique identifier of the matching document.</param>
/// <param name="Title">Title of the matching document.</param>
/// <param name="Snippet">Snippet of text highlighting the match.</param>
/// <param name="Score">Relevance score assigned by the search engine.</param>
public sealed record SearchResultItemResponse(Guid Id, string Title, string Snippet, double Score);
