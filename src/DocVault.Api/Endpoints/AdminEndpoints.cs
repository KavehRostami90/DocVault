using DocVault.Api.Composition;
using DocVault.Api.Contracts.Common;
using DocVault.Api.Contracts.Documents;
using DocVault.Api.Mappers;
using DocVault.Application.Common.Paging;
using DocVault.Application.UseCases.Documents.ListDocuments;
using DocVault.Infrastructure.Auth;
using Microsoft.AspNetCore.Identity;

namespace DocVault.Api.Endpoints;

public static class AdminEndpoints
{
  public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder routes)
  {
    var group = routes.MapGroup("/admin")
      .RequireAuthorization(AuthPolicies.RequireAdmin);

    group.MapGet("/documents", async (
      [AsParameters] ListDocumentsRequest request,
      ListDocumentsHandler handler,
      CancellationToken ct) =>
    {
      var result = await handler.HandleAsync(new ListDocumentsQuery(
        request.Page, request.Size, request.Sort, request.Desc,
        request.Title, request.Status, request.Tag,
        OwnerId: null, IsAdmin: true), ct);

      return Results.Ok(DocumentResponseMapper.ToPage(result));
    })
    .Produces<PageResponse<DocumentListItemResponse>>()
    .WithSummary("Admin: list all documents across all users");

    group.MapGet("/users", async (
      UserManager<ApplicationUser> users,
      CancellationToken ct) =>
    {
      var allUsers = users.Users
        .OrderByDescending(u => u.CreatedAt)
        .ToList();

      var result = new List<object>();
      foreach (var u in allUsers)
      {
        var roles = await users.GetRolesAsync(u);
        result.Add(new
        {
          id = u.Id,
          email = u.Email,
          displayName = u.DisplayName,
          isGuest = u.IsGuest,
          createdAt = u.CreatedAt,
          roles,
        });
      }

      return Results.Ok(result);
    })
    .WithSummary("Admin: list all registered users");

    return routes;
  }
}
