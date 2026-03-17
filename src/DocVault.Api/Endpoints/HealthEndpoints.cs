using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Text.Json;

namespace DocVault.Api.Endpoints;

/// <summary>
/// Maps health endpoints.
/// </summary>
public static class HealthEndpoints
{
  public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder routes)
  {
    // Liveness — is the process alive? No dependency checks.
    routes.MapHealthChecks("/health/live", new HealthCheckOptions
    {
      Predicate       = _ => false,
      ResponseWriter  = WriteJsonResponse,
    })
    .WithSummary("Liveness probe")
    .WithDescription("Returns 200 when the process is running. No dependency checks are performed.");

    // Readiness — are all dependencies reachable?
    routes.MapHealthChecks("/health/ready", new HealthCheckOptions
    {
      Predicate      = check => check.Tags.Contains("ready"),
      ResponseWriter = WriteJsonResponse,
    })
    .WithSummary("Readiness probe")
    .WithDescription("Returns 200 when all dependencies (database, storage) are reachable. Returns 503 when any check fails.");

    return routes;
  }

  // Writes a structured JSON body for both liveness and readiness responses.
  private static Task WriteJsonResponse(HttpContext ctx, HealthReport report)
  {
    ctx.Response.ContentType = "application/json; charset=utf-8";

    var payload = new
    {
      status        = report.Status.ToString(),
      totalDuration = report.TotalDuration.ToString("c"),
      checks        = report.Entries.ToDictionary(
        e => e.Key,
        e => new
        {
          status      = e.Value.Status.ToString(),
          description = e.Value.Description,
          duration    = e.Value.Duration.ToString("c"),
          error       = e.Value.Exception?.Message,
        }),
    };

    return ctx.Response.WriteAsync(
      JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = false }));
  }
}
