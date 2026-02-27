namespace DocVault.Api.Endpoints;

public static class HealthEndpoints
{
  public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder routes)
  {
    routes.MapGet("/health", () => Results.Ok(new { status = "ok" }));
    return routes;
  }
}
