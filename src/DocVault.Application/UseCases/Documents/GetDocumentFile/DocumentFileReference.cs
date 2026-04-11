namespace DocVault.Application.UseCases.Documents.GetDocumentFile;

public sealed record DocumentFileReference(string FileName, string ContentType, string StoragePath);
