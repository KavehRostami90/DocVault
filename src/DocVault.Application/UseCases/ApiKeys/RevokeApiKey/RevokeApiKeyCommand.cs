namespace DocVault.Application.UseCases.ApiKeys.RevokeApiKey;

/// <param name="Id">The API key to revoke.</param>
/// <param name="CallerUserId">The user making the request (for ownership check).</param>
/// <param name="IsAdmin">When true, ownership check is skipped.</param>
public sealed record RevokeApiKeyCommand(Guid Id, string CallerUserId, bool IsAdmin = false);
