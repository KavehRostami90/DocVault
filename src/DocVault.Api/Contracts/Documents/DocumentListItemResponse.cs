namespace DocVault.Api.Contracts.Documents;

/// <summary>
/// Document summary returned in list operations.
/// </summary>
/// <param name="Id">Unique identifier of the document.</param>
/// <param name="Title">Document title.</param>
/// <param name="FileName">Original file name of the document.</param>
/// <param name="Status">Processing status of the document.</param>
public sealed record DocumentListItemResponse(Guid Id, string Title, string FileName, string Status);
