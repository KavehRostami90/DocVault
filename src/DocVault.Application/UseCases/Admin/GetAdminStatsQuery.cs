namespace DocVault.Application.UseCases.Admin;

/// <summary>
/// Query that requests aggregate administration statistics
/// (user counts and document counts by status).
/// </summary>
/// <remarks>
/// User counts are fetched inside <see cref="GetAdminStatsHandler"/> via
/// <see cref="DocVault.Application.Abstractions.Users.IUserQueryService"/> —
/// no data needs to be supplied by the caller.
/// </remarks>
public sealed record GetAdminStatsQuery;