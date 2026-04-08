using DocVault.Domain.Documents;

namespace DocVault.Infrastructure.Persistence.Repositories;

/// <summary>
/// Computes a lightweight keyword-relevance score for a <see cref="Document"/>.
/// Title hits are weighted at 1.0 per term; body hits at 0.3 per term.
/// The result is normalised to the range [0, 1].
/// </summary>
internal static class DocumentScorer
{
  /// <summary>
  /// Scores <paramref name="document"/> against the given search <paramref name="terms"/>.
  /// </summary>
  /// <param name="document">The document to score.</param>
  /// <param name="terms">Whitespace-split search terms (already lower-cased by the caller).</param>
  /// <returns>A value in [0, 1]; higher means more relevant.</returns>
  public static double Compute(Document document, string[] terms)
  {
    var titleHits  = terms.Count(t => document.Title.Contains(t, StringComparison.OrdinalIgnoreCase));
    var textHits   = terms.Count(t => document.Text.Contains(t,  StringComparison.OrdinalIgnoreCase));
    var maxPossible = terms.Length * 1.3;
    return Math.Round((titleHits * 1.0 + textHits * 0.3) / maxPossible, 4);
  }
}
