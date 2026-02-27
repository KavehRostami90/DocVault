namespace DocVault.Api.Endpoints;

public static class TagsEndpoints
{
  public static IEndpointRouteBuilder MapTagsEndpoints(this IEndpointRouteBuilder routes)
  {
    var group = routes.MapGroup("/tags");
    group.MapGet("/", () => Results.Ok(Array.Empty<string>()));
    return routes;
  }
}
