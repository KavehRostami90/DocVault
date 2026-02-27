using System.Diagnostics;

namespace DocVault.Api.Middleware;

public class CorrelationIdMiddleware
{
  private const string HeaderName = "X-Correlation-Id";
  private readonly RequestDelegate _next;

  public CorrelationIdMiddleware(RequestDelegate next)
  {
    _next = next;
  }

  public async Task InvokeAsync(HttpContext context)
  {
    if (!context.Request.Headers.TryGetValue(HeaderName, out var correlationId))
    {
      correlationId = Activity.Current?.Id ?? Guid.NewGuid().ToString();
      context.Response.Headers.Append(HeaderName, correlationId);
    }
    context.Items[HeaderName] = correlationId.ToString();
    await _next(context);
  }
}
