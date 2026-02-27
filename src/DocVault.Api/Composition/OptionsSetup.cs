namespace DocVault.Api.Composition;

public static class OptionsSetup
{
  public static IServiceCollection AddApiOptions(this IServiceCollection services, IConfiguration configuration)
  {
    // Bind strongly typed options here.
    return services;
  }
}
