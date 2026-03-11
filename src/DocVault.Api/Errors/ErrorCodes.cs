namespace DocVault.Api.Errors;

internal static class ErrorCodes
{
  internal static class BadRequest
  {
    public const string MALFORMED_BODY    = "DV-400-BAD-REQUEST";
    public const string VALIDATION_FAILED = "DV-400-VALIDATION";
    public const string DOMAIN_VIOLATION  = "DV-400-DOMAIN";
  }

  internal static class Unauthorized
  {
    public const string AUTH_REQUIRED = "DV-401-UNAUTHORIZED";
  }

  internal static class Forbidden
  {
    public const string ACCESS_DENIED = "DV-403-FORBIDDEN";
  }

  internal static class NotFound
  {
    public const string RESOURCE_MISSING = "DV-404-NOT-FOUND";
  }

  internal static class Conflict
  {
    public const string DUPLICATE_RESOURCE = "DV-409-CONFLICT";
  }

  internal static class ServerError
  {
    public const string UNHANDLED        = "DV-500-UNHANDLED";
    public const string DATABASE_FAILURE = "DV-503-DATABASE";
    public const string EXTERNAL_SERVICE = "DV-503-EXTERNAL";
    public const string GATEWAY_TIMEOUT  = "DV-504-TIMEOUT";
  }
}
