using DocVault.Application;
using DocVault.Infrastructure;
using FluentValidation;
using DocVault.Api.Validation;
using DocVault.Api.Exceptions;

namespace DocVault.Api.Composition;

public static class DependencyInjection
{
  public static IServiceCollection AddDocVault(this IServiceCollection services, IConfiguration configuration)
  {
    services.AddApplication();
    services.AddInfrastructure(configuration);
    services.AddEndpointsApiExplorer();
    services.AddOpenApi();
    services.AddExceptionHandler<GlobalExceptionHandler>();
    services.AddProblemDetails();
    services.AddValidatorsFromAssemblyContaining<DocumentCreateRequestValidator>();
    return services;
  }
}
