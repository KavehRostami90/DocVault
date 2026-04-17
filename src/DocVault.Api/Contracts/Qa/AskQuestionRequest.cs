namespace DocVault.Api.Contracts.Qa;

public sealed record AskQuestionRequest(string Question, int MaxDocuments = 8, int MaxContexts = 6, Guid? DocumentId = null);
