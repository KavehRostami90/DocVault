using DocVault.Domain.Common;

namespace DocVault.Api.Composition;

public static class OptionsSetup
{
  public static IServiceCollection AddApiOptions(this IServiceCollection services, IConfiguration configuration)
  {
    services.AddOptions<UploadOptions>()
      .Bind(configuration.GetSection(UploadOptions.Section))
      .Validate(
        options => options.MaxFileSizeBytes >= ValidationConstants.Documents.MIN_FILE_SIZE_BYTES,
        $"Upload:{nameof(UploadOptions.MaxFileSizeBytes)} must be at least {ValidationConstants.Documents.MIN_FILE_SIZE_BYTES}.")
      .ValidateOnStart();

    return services;
  }
}
