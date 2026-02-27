namespace DocVault.Application.Common.Paging;

public sealed class Page<T>
{
  public IReadOnlyCollection<T> Items { get; }
  public int PageNumber { get; }
  public int PageSize { get; }
  public long TotalCount { get; }

  public Page(IEnumerable<T> items, int pageNumber, int pageSize, long totalCount)
  {
    Items = items.ToList();
    PageNumber = pageNumber;
    PageSize = pageSize;
    TotalCount = totalCount;
  }
}
