namespace DocVault.Application.UseCases.Qa;

public sealed record AskQuestionResult(
  string Answer,
  IReadOnlyList<AskQuestionCitation> Citations,
  bool AnsweredByModel);

public sealed record AskQuestionCitation(
  Guid DocumentId,
  string Title,
  string Excerpt,
  double Score);
