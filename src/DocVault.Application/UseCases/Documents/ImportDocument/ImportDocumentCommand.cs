using DocVault.Application.Common.Results;

namespace DocVault.Application.UseCases.Documents.ImportDocument;

public sealed record ImportDocumentCommand(string FileName, Stream Content);
