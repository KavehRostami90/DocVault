namespace DocVault.Api.Contracts.Documents;

/// <summary>
/// Details for a single document.
/// </summary>
/// <param name="Id">Unique identifier of the document.</param>
/// <param name="Title">Document title.</param>
/// <param name="FileName">Original file name of the document.</param>
/// <param name="ContentType">MIME content type of the stored file.</param>
/// <param name="Size">Size of the stored file in bytes.</param>
/// <param name="Status">Processing status of the document.</param>
/// <param name="Tags">Tags associated with the document.</param>
public sealed record DocumentReadResponse(Guid Id, string Title, string FileName, string ContentType, long Size, string Status, IReadOnlyCollection<string> Tags);
