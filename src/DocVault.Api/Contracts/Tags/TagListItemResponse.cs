namespace DocVault.Api.Contracts.Tags;

/// <summary>
/// Tag item returned from the tags endpoint.
/// </summary>
/// <param name="Name">Tag name.</param>
public sealed record TagListItemResponse(string Name);
