using System.Linq.Expressions;
using DocVault.Domain.Documents;

namespace DocVault.Infrastructure.Persistence.Repositories;

/// <summary>
/// Central registry of filter predicates applicable to <see cref="Document"/> queries.
/// Kept in a dedicated class so <see cref="EfDocumentRepository"/> does not need to
/// change when new filter keys are added (Open/Closed Principle).
/// </summary>
internal static class DocumentFilterRegistry
{
  /// <summary>
  /// Returns a dictionary mapping filter-key names to expression-factory delegates.
  /// Each factory receives the user-supplied filter value and returns an EF-translatable
  /// predicate over <see cref="Document"/>.
  /// </summary>
  public static IReadOnlyDictionary<string, Func<string, Expression<Func<Document, bool>>>> Build()
    => new Dictionary<string, Func<string, Expression<Func<Document, bool>>>>
    {
      ["title"] = value => d => d.Title.Contains(value),

      ["status"] = value =>
      {
        if (Enum.TryParse<DocumentStatus>(value, ignoreCase: true, out var status))
          return d => d.Status == status;

        // Unrecognised status → match nothing so consumers see an empty page.
        return d => false;
      },

      ["tag"] = value => d => d.Tags.Any(t => t.Name.Contains(value)),
    };
}
