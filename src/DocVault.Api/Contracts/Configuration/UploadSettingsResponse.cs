namespace DocVault.Api.Contracts.Configuration;

public sealed record UploadSettingsResponse(long MaxFileSizeBytes, int MaxUploadCount);
