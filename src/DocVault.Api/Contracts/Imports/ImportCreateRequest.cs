namespace DocVault.Api.Contracts.Imports;

/// <summary>
/// Payload for starting an import operation.
/// </summary>
/// <param name="FileName">Original file name of the import source.</param>
public sealed record ImportCreateRequest(string FileName);
