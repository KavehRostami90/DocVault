namespace DocVault.Api.Endpoints;

/// <summary>
/// Maps health endpoints.
/// </summary>
public static class HealthEndpoints
{
  public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder routes)
  {
    routes.MapGet("/health", () => Results.Ok(new { status = "ok" }))
      .Produces(StatusCodes.Status200OK)
      .WithSummary("Health check")
      .WithDescription("Returns OK when the API is reachable.");
    return routes;
  }
}
