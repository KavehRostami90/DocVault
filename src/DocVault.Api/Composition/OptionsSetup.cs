using DocVault.Domain.Common;
using DocVault.Infrastructure.Auth;

namespace DocVault.Api.Composition;

public static class OptionsSetup
{
  public static IServiceCollection AddApiOptions(
    this IServiceCollection services,
    IConfiguration configuration,
    IWebHostEnvironment environment)
  {
    // ── Upload ────────────────────────────────────────────────────────────────
    services.AddOptions<UploadOptions>()
      .Bind(configuration.GetSection(UploadOptions.Section))
      .Validate(
        o => o.MaxFileSizeBytes >= ValidationConstants.Documents.MIN_FILE_SIZE_BYTES,
        $"Upload:{nameof(UploadOptions.MaxFileSizeBytes)} must be at least {ValidationConstants.Documents.MIN_FILE_SIZE_BYTES} bytes.")
      .ValidateOnStart();

    // ── Auth ──────────────────────────────────────────────────────────────────
    // JwtSigningKey must be present and long enough for HMAC-SHA256 (≥ 256 bits = 32 ASCII chars).
    // AdminEmail / AdminPassword are required for the identity seeder that runs on first startup.
    services.AddOptions<AuthSettings>()
      .Bind(configuration.GetSection(AuthSettings.Section))
      .Validate(
        o => !string.IsNullOrWhiteSpace(o.JwtSigningKey),
        "Auth:JwtSigningKey is required. Set the DOCVAULT_JWT_KEY environment variable.")
      .Validate(
        o => string.IsNullOrWhiteSpace(o.JwtSigningKey) || o.JwtSigningKey.Length >= 32,
        "Auth:JwtSigningKey must be at least 32 characters (256 bits) for HMAC-SHA256.")
      .Validate(
        o => !string.IsNullOrWhiteSpace(o.AdminEmail),
        "Auth:AdminEmail is required. Set the DOCVAULT_ADMIN_EMAIL environment variable.")
      .Validate(
        o => !string.IsNullOrWhiteSpace(o.AdminPassword),
        "Auth:AdminPassword is required. Set the DOCVAULT_ADMIN_PASSWORD environment variable.")
      .Validate(
        o => string.IsNullOrWhiteSpace(o.AdminPassword) || o.AdminPassword.Length >= 8,
        "Auth:AdminPassword must be at least 8 characters.")
      .ValidateOnStart();

    // ── Database connection string ────────────────────────────────────────────
    // In Development the app intentionally falls back to an in-memory database so
    // developers can run without a local Postgres instance.  Outside Development
    // a missing connection string means the deployment is misconfigured.
    if (!environment.IsDevelopment())
    {
      services.AddOptions<DatabaseConnectionGuard>()
        .Configure(o => o.ConnectionString = configuration.GetConnectionString("Database"))
        .Validate(
          o => !string.IsNullOrWhiteSpace(o.ConnectionString),
          "ConnectionStrings:Database is required outside of Development. " +
          "Set the DOCVAULT_DB_* environment variables (see .env.example).")
        .ValidateOnStart();
    }

    return services;
  }

  // Thin wrapper so the connection string participates in the ValidateOnStart() pipeline.
  private sealed class DatabaseConnectionGuard
  {
    public string? ConnectionString { get; set; }
  }
}
