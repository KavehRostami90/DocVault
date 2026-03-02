using System.Diagnostics;
using Serilog.Context;

namespace DocVault.Api.Middleware;

public sealed class CorrelationIdMiddleware
{
  public const string HeaderName = "X-Correlation-Id";

  private readonly RequestDelegate _next;

  // ILogger not needed here — this middleware only pushes a property to the Serilog log context
  public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

  public async Task InvokeAsync(HttpContext context)
  {
    var correlationId = ResolveCorrelationId(context);

    context.Items[HeaderName] = correlationId;
    context.Response.OnStarting(() =>
    {
      context.Response.Headers[HeaderName] = correlationId;
      return Task.CompletedTask;
    });

    // PushProperty is the Serilog-idiomatic way to enrich all logs within this request scope
    using (LogContext.PushProperty("CorrelationId", correlationId))
    {
      await _next(context);
    }
  }

  private static string ResolveCorrelationId(HttpContext context)
  {
    if (context.Request.Headers.TryGetValue(HeaderName, out var existing) && !string.IsNullOrWhiteSpace(existing))
      return existing.ToString();

    var activityId = Activity.Current?.Id;
    return string.IsNullOrWhiteSpace(activityId) ? Guid.NewGuid().ToString() : activityId;
  }
}
