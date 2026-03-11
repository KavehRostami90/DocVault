namespace DocVault.Domain.Primitives;

public class DomainException : Exception
{
  /// <summary>
  /// Machine-readable error code that clients can switch on (e.g. "TITLE_INVALID", "TAG_LIMIT_EXCEEDED").
  /// </summary>
  public string Code { get; }

  public DomainException(string message) : base(message)
  {
    Code = "DOMAIN_ERROR";
  }

  public DomainException(string code, string message) : base(message)
  {
    Code = code;
  }
}

/// <summary>
/// Thrown when a business rule detects a duplicate / conflicting resource.
/// Mapped to HTTP 409 Conflict by the global exception handler.
/// </summary>
public class ConflictException : DomainException
{
  public ConflictException(string message) : base("CONFLICT", message) { }
  public ConflictException(string code, string message) : base(code, message) { }
}
