namespace DocVault.Application.UseCases.Qa;

/// <summary>
/// Asks a natural-language question over indexed document content.
/// </summary>
public sealed record AskQuestionQuery(
  string Question,
  int MaxDocuments = 8,
  int MaxContexts = 6,
  Guid? DocumentId = null,
  Guid? OwnerId = null,
  bool IsAdmin = false);
