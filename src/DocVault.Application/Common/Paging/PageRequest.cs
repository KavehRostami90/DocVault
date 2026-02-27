namespace DocVault.Application.Common.Paging;

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
    Size = Math.Max(1, size);
    Sort = string.IsNullOrWhiteSpace(sort) ? null : sort.Trim();
    Desc = desc;
    Filters = filters ?? new Dictionary<string, string?>();
  }
}
