using System.Linq.Expressions;

namespace DocVault.Application.Common.Filtering;

public static class FilterBuilder
{
  /// <summary>
  /// Applies string-based filters using a registry of builders keyed by filter name.
  /// </summary>
  public static IQueryable<T> Apply<T>(
    IQueryable<T> query,
    IReadOnlyDictionary<string, string?> filters,
    IReadOnlyDictionary<string, Func<string, Expression<Func<T, bool>>>> registry)
  {
    if (filters.Count == 0 || registry.Count == 0)
    {
      return query;
    }

    foreach (var (key, value) in filters)
    {
      if (string.IsNullOrWhiteSpace(value))
      {
        continue;
      }

      if (!registry.TryGetValue(key, out var builder))
      {
        continue;
      }

      var predicate = builder(value);
      query = query.Where(predicate);
    }

    return query;
  }
}
