using Asp.Versioning;
using DocVault.Application;
using DocVault.Infrastructure;
using DocVault.Infrastructure.Health;
using FluentValidation;
using DocVault.Api.Validation;
using DocVault.Api.Exceptions;
using System.Reflection;
using System.IO;

namespace DocVault.Api.Composition;

public static class DependencyInjection
{
  /// <summary>
  /// Registers API-layer services, OpenAPI support, exception handling, and validators.
  /// </summary>
  /// <param name="services">Service collection to populate.</param>
  /// <param name="configuration">Configuration source.</param>
  public static IServiceCollection AddDocVault(this IServiceCollection services, IConfiguration configuration)
  {
    services.AddApplication();
    services.AddInfrastructure(configuration);

    services.AddApiVersioning(options =>
    {
      options.DefaultApiVersion = new ApiVersion(1, 0);
      options.AssumeDefaultVersionWhenUnspecified = true;
      options.ReportApiVersions = true;
      options.ApiVersionReader = new UrlSegmentApiVersionReader();
    });

    services.AddEndpointsApiExplorer();
    // Named v1 OpenAPI document; served at /openapi/v1.json
    services.AddOpenApi("v1");
    services.AddExceptionHandler<GlobalExceptionHandler>();
    services.AddProblemDetails();
    services.AddValidatorsFromAssemblyContaining<DocumentCreateRequestValidator>();

    services.AddHealthChecks()
      .AddCheck<DatabaseHealthCheck>("database", tags: ["ready"])
      .AddCheck<StorageHealthCheck>("storage", tags: ["ready"]);

    return services;
  }
}
