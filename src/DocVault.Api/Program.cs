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

  // if (app.Environment.IsDevelopment())
  // {
  //   // OpenAPI document: /openapi/v1.json
  //   // Swagger UI:       /swagger
  //   // Scalar UI:        /scalar/v1
  //   app.UseSwaggerUI(c =>
  //   {
  //     c.SwaggerEndpoint("/openapi/v1.json", "DocVault API v1");
  //     c.RoutePrefix = "swagger";
  //     c.DocumentTitle = "DocVault API";
  //     c.DefaultModelExpandDepth(2);
  //     c.DisplayRequestDuration();
  //   });
  // }

  app.UseSwaggerUI(c =>
  {
    c.SwaggerEndpoint("/openapi/v1.json", "DocVault API v1");
    c.RoutePrefix = "swagger";
    c.DocumentTitle = "DocVault API";
    c.DefaultModelExpandDepth(2);
    c.DisplayRequestDuration();
  });

  app.MapOpenApi();
  app.MapScalarApiReference();
  app.MapDocumentsEndpoints();
  app.MapSearchEndpoints();
  app.MapTagsEndpoints();
  app.MapImportsEndpoints();
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
