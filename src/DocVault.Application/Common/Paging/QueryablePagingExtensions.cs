using System.Linq.Expressions;

namespace DocVault.Application.Common.Paging;

public static class QueryablePagingExtensions
{
  public static IQueryable<T> Page<T>(this IQueryable<T> query, PageRequest request)
    => query.Skip((request.Page - 1) * request.Size).Take(request.Size);

  public static async Task<Page<T>> ToPageAsync<T>(this IQueryable<T> query, PageRequest request, CancellationToken cancellationToken = default)
  {
    var total = query.LongCount();
    var items = query.Page(request).ToList();
    return new Page<T>(items, request.Page, request.Size, total);
  }
}
