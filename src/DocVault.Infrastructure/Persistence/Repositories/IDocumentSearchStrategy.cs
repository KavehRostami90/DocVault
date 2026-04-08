using DocVault.Application.UseCases.Search;
using DocVault.Application.Common.Paging;

namespace DocVault.Infrastructure.Persistence.Repositories;

/// <summary>
/// Strategy that executes a keyword search against a <see cref="DocVaultDbContext"/>.
/// Implementations choose the appropriate engine (Postgres FTS or in-memory LIKE)
/// based on the provider in use.
/// </summary>
internal interface IDocumentSearchStrategy
{
  /// <summary>
  /// Returns <c>true</c> when this strategy can handle the active database provider.
  /// The first matching strategy (by registration order) wins.
  /// </summary>
  bool CanHandle(DocVaultDbContext db);

  /// <summary>Executes the search and returns a paginated result set.</summary>
  Task<Page<SearchResultItem>> SearchAsync(
    DocVaultDbContext db,
    string[] terms,
    int page,
    int size,
    Guid? ownerId,
    CancellationToken ct);
}
