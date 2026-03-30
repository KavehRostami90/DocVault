using Asp.Versioning;
using DocVault.Api.Middleware;
using DocVault.Application;
using DocVault.Infrastructure;
using DocVault.Infrastructure.Health;
using FluentValidation;
using DocVault.Api.Validation;
using DocVault.Api.Exceptions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using System.Threading.RateLimiting;

namespace DocVault.Api.Composition;

public static class DependencyInjection
{
  public static IServiceCollection AddDocVault(this IServiceCollection services, IConfiguration configuration)
  {
    services.AddApplication();
    services.AddInfrastructure(configuration);

    // CORS — origins driven by config so the value can be tightened per-environment
    var rawOrigins = configuration["Cors:AllowedOrigins"] ?? string.Empty;
    var origins = rawOrigins.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    services.AddCors(options => options.AddDefaultPolicy(policy =>
    {
      if (origins.Length == 0 || origins.Contains("*"))
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
      else
        policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod();
    }));

    services.AddApiVersioning(options =>
    {
      options.DefaultApiVersion = new ApiVersion(1, 0);
      options.AssumeDefaultVersionWhenUnspecified = true;
      options.ReportApiVersions = true;
      options.ApiVersionReader = new UrlSegmentApiVersionReader();
    });

    services.AddRateLimiter(options =>
    {
      options.AddPolicy(RateLimitPolicies.DocumentUpload, httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
          partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
          factory: _ => new FixedWindowRateLimiterOptions
          {
            PermitLimit          = 20,
            Window               = TimeSpan.FromMinutes(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit           = 0,
          }));

      options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    });

    services.AddEndpointsApiExplorer();
    services.AddOpenApi("v1");
    services.AddExceptionHandler<GlobalExceptionHandler>();
    services.AddProblemDetails();
    services.AddValidatorsFromAssemblyContaining<DocumentCreateRequestValidator>();

    var authOptions = configuration.GetSection(AuthOptions.Section).Get<AuthOptions>() ?? new AuthOptions();
    if (authOptions.IsConfigured)
    {
      services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
          options.Authority = authOptions.Authority;
          options.Audience  = authOptions.Audience;
          options.MapInboundClaims = false;
        });
      services.AddAuthorization(options =>
      {
        options.AddPolicy(AuthPolicies.ReadDocuments,  p => p.RequireClaim("scope", "documents:read"));
        options.AddPolicy(AuthPolicies.WriteDocuments, p => p.RequireClaim("scope", "documents:write"));
      });
    }
    else
    {
      services.AddAuthentication();
      services.AddAuthorization();
    }

    services.AddHealthChecks()
      .AddCheck<DatabaseHealthCheck>("database", tags: ["ready"])
      .AddCheck<StorageHealthCheck>("storage", tags: ["ready"]);

    return services;
  }
}
