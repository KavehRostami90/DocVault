namespace DocVault.Application.Abstractions.Qa;

/// <summary>
/// Generates an answer from a user question and a set of retrieved context chunks.
/// </summary>
public interface IQuestionAnsweringService
{
  Task<QaAnswerResult> AnswerAsync(string question, IReadOnlyList<QaContextChunk> contexts, CancellationToken cancellationToken = default);
}

/// <summary>
/// Retrieved context unit used by the QA/RAG stage.
/// </summary>
public sealed record QaContextChunk(Guid DocumentId, string DocumentTitle, string Text, double RetrievalScore, int Rank);

/// <summary>
/// QA output produced by the configured generator.
/// </summary>
public sealed record QaAnswerResult(string Answer, bool AnsweredByModel);
