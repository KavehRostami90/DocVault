namespace DocVault.Api.Contracts.Common;

/// <summary>
/// Key/value filter criteria to apply when listing resources.
/// </summary>
/// <param name="Criteria">Key/value pairs where the key is the field name and the value is the filter term.</param>
public sealed record FilterSpec(IDictionary<string, string> Criteria);
