namespace DocVault.Api.Errors;

internal static class ErrorCodes
{
  public const string ValidationFailed = "DV-400-VALIDATION";
  public const string DomainRuleViolation = "DV-400-DOMAIN";
  public const string NotFound = "DV-404-NOT-FOUND";
  public const string Conflict = "DV-409-CONFLICT";
  public const string Unauthorized = "DV-401-UNAUTHORIZED";
  public const string Forbidden = "DV-403-FORBIDDEN";
  public const string DatabaseFailure = "DV-503-DATABASE";
  public const string ExternalServiceFailure = "DV-503-EXTERNAL";
  public const string Unhandled = "DV-500-UNHANDLED";
}
