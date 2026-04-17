namespace DocVault.Api.Middleware;

public static class RateLimitPolicies
{
  public const string DocumentUpload = "document-upload";
  public const string AuthEndpoints  = "auth-endpoints";
  public const string Search         = "search";
  public const string Qa             = "qa";
}
