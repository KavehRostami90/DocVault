using DocVault.Api.Composition;
using DocVault.Api.Endpoints;
using DocVault.Api.Middleware;
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

  // CorrelationId must be first so all downstream logs include it
  app.UseMiddleware<CorrelationIdMiddleware>();
  app.UseExceptionHandler();

  // Serilog request logging replaces hand-rolled RequestLoggingMiddleware
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

  if (app.Environment.IsDevelopment())
  {
    app.MapOpenApi();
  }

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
