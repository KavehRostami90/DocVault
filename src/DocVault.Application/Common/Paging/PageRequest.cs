using DocVault.Domain.Common;

namespace DocVault.Application.Common.Paging;

/// <summary>
/// Encapsulates pagination, sorting, and filter parameters for a list query.
/// </summary>
/// <remarks>
/// Invalid values are <em>normalised</em> rather than rejected so that query-string
/// sources (e.g. <c>?page=0&amp;size=999</c>) never produce 400 errors for borderline
/// inputs.  Consumers that want strict validation should apply it before constructing
/// a <see cref="PageRequest"/> (e.g. via FluentValidation in the API layer).
/// <list type="bullet">
/// <item><description><c>page</c> is clamped to a minimum of 1.</description></item>
/// <item><description><c>size</c> is clamped to [1, <see cref="ValidationConstants.Paging.MAX_PAGE_SIZE"/>].</description></item>
/// </list>
/// </remarks>
public sealed class PageRequest
{
  public int Page { get; }
  public int Size { get; }
  public string? Sort { get; }
  public bool Desc { get; }
  public IReadOnlyDictionary<string, string?> Filters { get; }

  public PageRequest(int page = 1, int size = 20, string? sort = null, bool desc = false, IReadOnlyDictionary<string, string?>? filters = null)
  {
    Page = Math.Max(1, page);
    Size = Math.Clamp(size, 1, ValidationConstants.Paging.MAX_PAGE_SIZE);
    Sort = string.IsNullOrWhiteSpace(sort) ? null : sort.Trim();
    Desc = desc;
    Filters = filters ?? new Dictionary<string, string?>();
  }
}
