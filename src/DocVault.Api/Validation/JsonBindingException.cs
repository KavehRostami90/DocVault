namespace DocVault.Api.Validation;

/// <summary>
/// Thrown during custom JSON body binding when one or more properties
/// cannot be deserialized to their declared types.
/// Carries all per-property errors so the full set is returned in one response.
/// </summary>
public sealed class JsonBindingException : BadHttpRequestException
{
  public IReadOnlyDictionary<string, string[]> Errors { get; }

  public JsonBindingException(IReadOnlyDictionary<string, string[]> errors)
    : base("One or more JSON binding errors occurred.")
  {
    Errors = errors;
  }
}
