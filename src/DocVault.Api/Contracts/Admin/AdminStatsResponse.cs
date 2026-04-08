namespace DocVault.Api.Contracts.Admin;

/// <summary>
/// Aggregate statistics returned by the admin stats endpoint.
/// </summary>
/// <param name="TotalUsers">Total number of registered user accounts.</param>
/// <param name="GuestUsers">Number of guest (anonymous) user accounts.</param>
/// <param name="RegisteredUsers">Number of fully-registered (non-guest) user accounts.</param>
/// <param name="AdminUsers">Number of users that hold the Admin role.</param>
/// <param name="TotalDocuments">Total number of documents across all users.</param>
/// <param name="DocumentsByStatus">Document counts keyed by processing status name (e.g. "Pending", "Indexed").</param>
public sealed record AdminStatsResponse(
  int TotalUsers,
  int GuestUsers,
  int RegisteredUsers,
  int AdminUsers,
  long TotalDocuments,
  Dictionary<string, long> DocumentsByStatus);
