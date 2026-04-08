namespace DocVault.Api.Contracts.Admin;

/// <summary>
/// Request body for replacing all roles assigned to a user.
/// </summary>
/// <param name="Roles">
/// The complete set of role names to assign to the user.
/// Any roles the user currently holds that are not in this list will be removed.
/// </param>
public sealed record UpdateUserRolesRequest(string[] Roles);
