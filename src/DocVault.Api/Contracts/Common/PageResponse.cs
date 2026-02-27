namespace DocVault.Api.Contracts.Common;

public sealed record PageResponse<T>(IReadOnlyCollection<T> Items, int Page, int Size, long TotalCount);
