using DocVault.Application;
using DocVault.Infrastructure;
using FluentValidation;
using DocVault.Api.Validation;
using DocVault.Api.Exceptions;
using System.Reflection;
using System.IO;

namespace DocVault.Api.Composition;

public static class DependencyInjection
{
  public static IServiceCollection AddDocVault(this IServiceCollection services, IConfiguration configuration)
  {
    services.AddApplication();
    services.AddInfrastructure(configuration);
    services.AddEndpointsApiExplorer();
    // Built-in OpenAPI in ASP.NET Core 10 does not expose XML comment inclusion; rely on summaries/descriptions on endpoints/DTOs.
    services.AddOpenApi();
    services.AddExceptionHandler<GlobalExceptionHandler>();
    services.AddProblemDetails();
    services.AddValidatorsFromAssemblyContaining<DocumentCreateRequestValidator>();
    return services;
  }
}
