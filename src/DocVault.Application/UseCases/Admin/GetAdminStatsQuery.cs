namespace DocVault.Application.UseCases.Admin;

/// <summary>
/// Query that requests aggregate administration statistics
/// (user counts and document counts by status).
/// </summary>
/// <remarks>
/// User counts are not resolved by this query — they must be supplied as
/// explicit parameters to <see cref="GetAdminStatsHandler.HandleAsync"/> because
/// <c>UserManager</c> lives in the Infrastructure layer and cannot be injected
/// into Application handlers.
/// </remarks>
public sealed record GetAdminStatsQuery;
