using DocVault.Application.Common.Paging;

namespace DocVault.Application.UseCases.Documents.ListDocuments;

public sealed record ListDocumentsQuery(
	int Page = 1,
	int Size = 20,
	string? Sort = null,
	bool Desc = false,
	string? Title = null,
	string? Status = null,
	string? Tag = null);
