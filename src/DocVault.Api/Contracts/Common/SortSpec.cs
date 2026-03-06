namespace DocVault.Api.Contracts.Common;

/// <summary>
/// Sort instructions applied to list results.
/// </summary>
/// <param name="Field">Field name to sort by.</param>
/// <param name="Descending">True for descending order; false for ascending.</param>
public sealed record SortSpec(string Field, bool Descending = false);
