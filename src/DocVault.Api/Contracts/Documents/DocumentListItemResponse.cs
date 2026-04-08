namespace DocVault.Api.Contracts.Documents;

/// <summary>
/// Document summary returned in list operations.
/// </summary>
/// <param name="Id">Unique identifier of the document.</param>
/// <param name="Title">Document title.</param>
/// <param name="FileName">Original file name of the document.</param>
/// <param name="Status">Processing status of the document.</param>
/// <param name="Size">File size in bytes.</param>
/// <param name="CreatedAt">UTC timestamp when the document was created.</param>
/// <param name="OwnerId">Identity of the user who owns the document, if any.</param>
public sealed record DocumentListItemResponse(Guid Id, string Title, string FileName, string Status, long Size, DateTime CreatedAt, Guid? OwnerId);
