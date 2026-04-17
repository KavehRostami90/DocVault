namespace DocVault.Api.Contracts.Qa;

public sealed record AskQuestionResponse(string Answer, bool AnsweredByModel, IReadOnlyList<AskQuestionCitationResponse> Citations);

public sealed record AskQuestionCitationResponse(Guid DocumentId, string Title, string Excerpt, double Score);
