using System.Text;
using Asp.Versioning;
using DocVault.Api.Exceptions;
using DocVault.Api.Middleware;
using DocVault.Api.Services;
using DocVault.Api.Validation;
using DocVault.Application;
using DocVault.Application.Abstractions.Auth;
using DocVault.Infrastructure;
using DocVault.Infrastructure.Auth;
using DocVault.Infrastructure.Health;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Threading.RateLimiting;

namespace DocVault.Api.Composition;

public static class DependencyInjection
{
  public static IServiceCollection AddDocVault(this IServiceCollection services, IConfiguration configuration)
  {
    services.AddInfrastructure(configuration);
    services.AddApplication();

    // CORS — allow credentials when specific origins are configured (required for httpOnly cookie cross-origin)
    var rawOrigins = configuration["Cors:AllowedOrigins"] ?? string.Empty;
    var origins = rawOrigins.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    services.AddCors(options => options.AddDefaultPolicy(policy =>
    {
      if (origins.Length == 0 || origins.Contains("*"))
        // AllowAnyOrigin() cannot be combined with AllowCredentials() (browser hard-blocks it).
        // SetIsOriginAllowed echoes back the request Origin, satisfying the browser.
        // Only used when no explicit origins are configured (dev / unconfigured deployments).
        policy.SetIsOriginAllowed(_ => true).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
      else
        policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
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

      // Stricter limit on auth endpoints to mitigate brute-force attacks.
      options.AddPolicy(RateLimitPolicies.AuthEndpoints, httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
          partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
          factory: _ => new FixedWindowRateLimiterOptions
          {
            PermitLimit          = 10,
            Window               = TimeSpan.FromMinutes(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit           = 0,
          }));

      options.AddPolicy(RateLimitPolicies.Search, httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
          partitionKey: httpContext.User.Identity?.Name ?? httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
          factory: _ => new FixedWindowRateLimiterOptions
          {
            PermitLimit          = 60,
            Window               = TimeSpan.FromMinutes(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit           = 0,
          }));

      options.AddPolicy(RateLimitPolicies.Qa, httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
          partitionKey: httpContext.User.Identity?.Name ?? httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
          factory: _ => new FixedWindowRateLimiterOptions
          {
            PermitLimit          = 20,
            Window               = TimeSpan.FromMinutes(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit           = 0,
          }));

      options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    });

    // JWT Bearer authentication
    var authSettings = configuration.GetSection(AuthSettings.Section).Get<AuthSettings>() ?? new AuthSettings();
    if (authSettings.IsConfigured)
    {
      services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
          options.TokenValidationParameters = new TokenValidationParameters
          {
            ValidateIssuer = true,
            ValidIssuer = authSettings.JwtIssuer,
            ValidateAudience = true,
            ValidAudience = authSettings.JwtAudience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(authSettings.JwtSigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
          };
          options.MapInboundClaims = false;
        });
    }
    else
    {
      services.AddAuthentication();
    }

    services.AddAuthorization(options =>
    {
      options.AddPolicy(AuthPolicies.RequireAdmin, p => p.RequireRole(AppRoles.Admin));
      options.AddPolicy(AuthPolicies.RequireUser,  p => p.RequireRole(AppRoles.Admin, AppRoles.User, AppRoles.Guest));
    });

    // Current user context — resolves from the JWT claims in the active request
    services.AddHttpContextAccessor();
    services.AddScoped<ICurrentUser, CurrentUserService>();

    services.AddEndpointsApiExplorer();
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
