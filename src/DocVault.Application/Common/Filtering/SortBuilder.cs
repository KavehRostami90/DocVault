using System.Linq.Expressions;

namespace DocVault.Application.Common.Filtering;

public static class SortBuilder
{
  public static IQueryable<T> Apply<T, TDefault>(
    IQueryable<T> query,
    string? sort,
    bool desc,
    IReadOnlyDictionary<string, Expression<Func<T, object>>> registry,
    Expression<Func<T, TDefault>> defaultSort)
  {
    if (!string.IsNullOrWhiteSpace(sort) && registry.TryGetValue(sort, out var selector))
    {
      return desc ? query.OrderByDescending(selector) : query.OrderBy(selector);
    }

    // Default sort if none supplied or unknown key
    return query.OrderByDescending(defaultSort);
  }
}
