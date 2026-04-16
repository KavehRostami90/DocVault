using DocVault.Api.Contracts.Tags;
using DocVault.Application.Abstractions.Auth;
using DocVault.Application.UseCases.Tags.ListTags;

namespace DocVault.Api.Endpoints;

/// <summary>
/// Maps tag endpoints.
/// </summary>
public static class TagsEndpoints
{
  public static IEndpointRouteBuilder MapTagsEndpoints(this IEndpointRouteBuilder routes)
  {
    var group = routes.MapGroup("/tags")
      .RequireAuthorization();
    group.MapGet("/", async (ListTagsHandler handler, ICurrentUser currentUser, CancellationToken ct) =>
      {
        var ownerId = currentUser.IsAdmin ? null : currentUser.UserId;
        var names = await handler.HandleAsync(ownerId, ct);
        var dto = names.Select(n => new TagListItemResponse(n)).ToArray();
        return Results.Ok(dto);
      })
      .Produces<TagListItemResponse[]>(StatusCodes.Status200OK)
      .WithSummary("List tags")
      .WithDescription("Returns all available tags.");
    return routes;
  }
}
