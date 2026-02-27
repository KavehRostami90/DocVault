namespace DocVault.Api.Contracts.Common;

public sealed record PageRequest(int Page = 1, int Size = 20, SortSpec? Sort = null, FilterSpec? Filter = null);
