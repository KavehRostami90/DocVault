using DocVault.Api.Composition;
using DocVault.Api.Contracts.Admin;
using DocVault.Api.Contracts.Common;
using DocVault.Api.Contracts.Documents;
using DocVault.Api.Mappers;
using DocVault.Application.Common.Paging;
using DocVault.Application.UseCases.Admin;
using DocVault.Application.UseCases.Documents.DeleteDocument;
using DocVault.Application.UseCases.Documents.ListDocuments;
using DocVault.Domain.Documents;
using DocVault.Infrastructure.Auth;
using Microsoft.AspNetCore.Identity;

namespace DocVault.Api.Endpoints;

/// <summary>
/// Registers all <c>/admin/*</c> endpoint routes. Every route requires the
/// <see cref="AuthPolicies.RequireAdmin"/> authorisation policy.
/// </summary>
public static class AdminEndpoints
{
  /// <summary>
  /// Maps admin routes onto <paramref name="routes"/> under the <c>/admin</c> prefix.
  /// </summary>
  public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder routes)
  {
    var group = routes.MapGroup("/admin")
      .RequireAuthorization(AuthPolicies.RequireAdmin);

    // ── Documents ────────────────────────────────────────────────────────────

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

    group.MapDelete("/documents/{id:guid}", async (
      Guid id,
      DeleteDocumentHandler handler,
      CancellationToken ct) =>
    {
      var result = await handler.HandleAsync(
        new DeleteDocumentCommand(new DocumentId(id), CallerId: null, IsAdmin: true), ct);
      return result.IsSuccess ? Results.NoContent() : Results.NotFound();
    })
    .Produces(StatusCodes.Status204NoContent)
    .Produces(StatusCodes.Status404NotFound)
    .WithSummary("Admin: delete any document");

    group.MapPost("/documents/{id:guid}/reindex", async (
      Guid id,
      ReindexDocumentHandler handler,
      CancellationToken ct) =>
    {
      var result = await handler.HandleAsync(new ReindexDocumentCommand(new DocumentId(id)), ct);
      return result.IsSuccess ? Results.NoContent() : Results.NotFound();
    })
    .Produces(StatusCodes.Status204NoContent)
    .Produces(StatusCodes.Status404NotFound)
    .WithSummary("Admin: re-queue a document for indexing");

    // ── Users ─────────────────────────────────────────────────────────────────

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

    group.MapDelete("/users/{id}", async (
      string id,
      UserManager<ApplicationUser> users,
      CancellationToken ct) =>
    {
      var user = await users.FindByIdAsync(id);
      if (user is null) return Results.NotFound();

      var result = await users.DeleteAsync(user);
      return result.Succeeded ? Results.NoContent() : Results.Problem(
        detail: string.Join("; ", result.Errors.Select(e => e.Description)),
        statusCode: StatusCodes.Status422UnprocessableEntity);
    })
    .Produces(StatusCodes.Status204NoContent)
    .Produces(StatusCodes.Status404NotFound)
    .WithSummary("Admin: delete a user");

    group.MapPut("/users/{id}/roles", async (
      string id,
      UpdateUserRolesRequest request,
      UserManager<ApplicationUser> users,
      CancellationToken ct) =>
    {
      var user = await users.FindByIdAsync(id);
      if (user is null) return Results.NotFound();

      var current = await users.GetRolesAsync(user);
      await users.RemoveFromRolesAsync(user, current);
      var addResult = await users.AddToRolesAsync(user, request.Roles);

      return addResult.Succeeded ? Results.NoContent() : Results.Problem(
        detail: string.Join("; ", addResult.Errors.Select(e => e.Description)),
        statusCode: StatusCodes.Status422UnprocessableEntity);
    })
    .Produces(StatusCodes.Status204NoContent)
    .Produces(StatusCodes.Status404NotFound)
    .WithSummary("Admin: assign roles to a user");

    // ── Stats ─────────────────────────────────────────────────────────────────

    group.MapGet("/stats", async (
      GetAdminStatsHandler handler,
      UserManager<ApplicationUser> users,
      CancellationToken ct) =>
    {
      var allUsers = users.Users.ToList();
      int guestCount = 0, adminCount = 0;
      foreach (var u in allUsers)
      {
        var roles = await users.GetRolesAsync(u);
        if (u.IsGuest) guestCount++;
        if (roles.Contains(AppRoles.Admin)) adminCount++;
      }

      var result = await handler.HandleAsync(
        new GetAdminStatsQuery(),
        totalUsers: allUsers.Count,
        guestUsers: guestCount,
        registeredUsers: allUsers.Count - guestCount,
        adminUsers: adminCount,
        ct);

      var dto = result.Value!;
      return Results.Ok(new AdminStatsResponse(
        dto.TotalUsers,
        dto.GuestUsers,
        dto.RegisteredUsers,
        dto.AdminUsers,
        dto.TotalDocuments,
        dto.DocumentsByStatus));
    })
    .Produces<AdminStatsResponse>()
    .WithSummary("Admin: aggregate stats");

    return routes;
  }
}
