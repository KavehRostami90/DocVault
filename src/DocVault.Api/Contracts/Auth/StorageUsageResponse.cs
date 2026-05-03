namespace DocVault.Api.Contracts.Auth;

public sealed record StorageUsageResponse(long UsedBytes, long DocumentCount);
