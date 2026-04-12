using DocVault.Api.Composition;
using DocVault.Api.Contracts.Configuration;
using Microsoft.Extensions.Options;

namespace DocVault.Api.Endpoints;

public static class ConfigurationEndpoints
{
  public static IEndpointRouteBuilder MapConfigurationEndpoints(this IEndpointRouteBuilder routes)
  {
    routes.MapGet("/config/upload", (IOptions<UploadOptions> options) =>
      Results.Ok(new UploadSettingsResponse(options.Value.MaxFileSizeBytes, options.Value.MaxUploadCount)))
      .Produces<UploadSettingsResponse>(StatusCodes.Status200OK)
      .WithSummary("Get public upload settings")
      .WithDescription("Returns public upload limits used by the client UI.");

    return routes;
  }
}
