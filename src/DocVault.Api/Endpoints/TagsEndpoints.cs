using DocVault.Api.Contracts.Tags;
using DocVault.Application.UseCases.Tags.ListTags;
using DocVault.Application.Abstractions.Persistence;

namespace DocVault.Api.Endpoints;

public static class TagsEndpoints
{
  public static IEndpointRouteBuilder MapTagsEndpoints(this IEndpointRouteBuilder routes)
  {
    var group = routes.MapGroup("/tags");
    group.MapGet("/", async (ListTagsHandler handler, CancellationToken ct) =>
      {
        var names = await handler.HandleAsync(ct);
        var dto = names.Select(n => new TagListItemResponse(n)).ToArray();
        return Results.Ok(dto);
      })
      .Produces<TagListItemResponse[]>(StatusCodes.Status200OK)
      .WithSummary("List tags")
      .WithDescription("Returns all available tags.");
    return routes;
  }
}
