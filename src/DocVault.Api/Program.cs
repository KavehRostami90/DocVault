using Asp.Versioning;
using DocVault.Api.Composition;
using DocVault.Api.Endpoints;
using DocVault.Api.Middleware;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Events;

// Minimal bootstrap logger captures startup errors before full config loads
Log.Logger = new LoggerConfiguration()
  .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
  .Enrich.FromLogContext()
  .WriteTo.Console()
  .CreateBootstrapLogger();

try
{
  Log.Information("Starting DocVault API host");

  var builder = WebApplication.CreateBuilder(args);

  // Full Serilog config is read from appsettings; ReadFrom.Services allows DI-registered enrichers/sinks
  builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services));

  builder.Services.AddDocVault(builder.Configuration);
  builder.Services.AddApiOptions(builder.Configuration);

  var app = builder.Build();

  // 1. Serilog outermost — wraps every request so all logs carry request context
  app.UseSerilogRequestLogging(options =>
  {
    options.GetLevel = (httpContext, _, ex) => ex is not null
      ? LogEventLevel.Error
      : httpContext.Response.StatusCode >= 500
        ? LogEventLevel.Error
        : httpContext.Response.StatusCode >= 400
          ? LogEventLevel.Warning
          : LogEventLevel.Information;

    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
      diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value ?? string.Empty);
      diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
      if (httpContext.Items.TryGetValue(CorrelationIdMiddleware.HeaderName, out var correlationId))
        diagnosticContext.Set("CorrelationId", correlationId?.ToString() ?? string.Empty);
    };
  });

  // 2. CorrelationId — pushes property into Serilog LogContext for all downstream logs
  app.UseMiddleware<CorrelationIdMiddleware>();

  // 3. ExceptionHandler — translates unhandled exceptions to problem+json responses
  app.UseExceptionHandler();

  // 4. Rate limiting — enforced before endpoint routing
  app.UseRateLimiter();

  // 5. Authentication / Authorization
  app.UseAuthentication();
  app.UseAuthorization();

  if (app.Environment.IsDevelopment())
  {
    app.UseSwaggerUI(c =>
    {
      c.SwaggerEndpoint("/openapi/v1.json", "DocVault API v1");
      c.RoutePrefix = "swagger";
      c.DocumentTitle = "DocVault API";
      c.DefaultModelExpandDepth(2);
      c.DisplayRequestDuration();
    });
  }

  app.MapOpenApi();
  app.MapScalarApiReference();

  // All versioned API endpoints live under /api/v{n}
  // Health endpoints are infrastructure concerns and intentionally unversioned
  var apiVersionSet = app.NewApiVersionSet()
    .HasApiVersion(new ApiVersion(1, 0))
    .ReportApiVersions()
    .Build();

  var v1 = app.MapGroup("/api/v{version:apiVersion}")
    .WithApiVersionSet(apiVersionSet);

  v1.MapDocumentsEndpoints();
  v1.MapSearchEndpoints();
  v1.MapTagsEndpoints();
  v1.MapImportsEndpoints();
  app.MapHealthEndpoints();

  app.Run();
}
catch (Exception ex)
{
  Log.Fatal(ex, "Host terminated unexpectedly");
}
finally
{
  Log.CloseAndFlush();
}

// Exposes the implicit top-level Program class to the integration test project
public partial class Program { }
