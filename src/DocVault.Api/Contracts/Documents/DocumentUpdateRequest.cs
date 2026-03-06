namespace DocVault.Api.Contracts.Documents;

/// <summary>
/// Payload for updating document metadata.
/// </summary>
/// <param name="Title">Updated title for the document.</param>
/// <param name="Tags">Tags that should replace the existing set.</param>
public sealed record DocumentUpdateRequest(string Title, IReadOnlyCollection<string> Tags);
