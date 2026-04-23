using DocVault.Api.Contracts.Tags;
using DocVault.Application.Abstractions.Auth;
using DocVault.Application.UseCases.Tags.ListTags;

namespace DocVault.Api.Endpoints;

public static class TagsEndpoints
{
  public static IEndpointRouteBuilder MapTagsEndpoints(this IEndpointRouteBuilder routes)
  {
    var group = routes.MapGroup("/tags")
      .RequireAuthorization();

    group.MapGet("/", async (ListTagsHandler handler, ICurrentUser currentUser, CancellationToken ct) =>
    {
      var query = new ListTagsQuery(OwnerId: currentUser.UserId, IsAdmin: currentUser.IsAdmin);
      var names = await handler.HandleAsync(query, ct);
      return Results.Ok(names.Select(n => new TagListItemResponse(n)).ToArray());
    })
    .Produces<TagListItemResponse[]>(StatusCodes.Status200OK)
    .WithSummary("List tags")
    .WithDescription("Returns all available tags.");

    return routes;
  }
}
