using DocVault.Api.Composition;
using DocVault.Api.Contracts.Admin;
using DocVault.Api.Contracts.Common;
using DocVault.Api.Validation;
using DocVault.Api.Contracts.Documents;
using DocVault.Api.Mappers;
using DocVault.Application.Abstractions.Auth;
using DocVault.Application.Abstractions.Storage;
using DocVault.Application.UseCases.Admin;
using DocVault.Application.UseCases.Documents.DeleteDocument;
using DocVault.Application.UseCases.Documents.GetDocumentFile;
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
  private const string AuditLoggerName = "DocVault.Admin.Audit";

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
      ICurrentUser caller,
      ILoggerFactory loggerFactory,
      DeleteDocumentHandler handler,
      CancellationToken ct) =>
    {
      var logger = loggerFactory.CreateLogger(AuditLoggerName);
      var result = await handler.HandleAsync(
        new DeleteDocumentCommand(new DocumentId(id), CallerId: null, IsAdmin: true), ct);

      if (result.IsSuccess)
        logger.LogWarning("Admin {CallerId} deleted document {DocumentId}", caller.UserId, id);

      return result.IsSuccess ? Results.NoContent() : Results.NotFound();
    })
    .Produces(StatusCodes.Status204NoContent)
    .Produces(StatusCodes.Status404NotFound)
    .WithSummary("Admin: delete any document");

    group.MapPost("/documents/{id:guid}/reindex", async (
      Guid id,
      ICurrentUser caller,
      ILoggerFactory loggerFactory,
      ReindexDocumentHandler handler,
      CancellationToken ct) =>
    {
      var logger = loggerFactory.CreateLogger(AuditLoggerName);
      var result = await handler.HandleAsync(new ReindexDocumentCommand(new DocumentId(id)), ct);

      if (result.IsSuccess)
        logger.LogInformation("Admin {CallerId} re-indexed document {DocumentId}", caller.UserId, id);

      return result.IsSuccess ? Results.NoContent() : Results.NotFound();
    })
    .Produces(StatusCodes.Status204NoContent)
    .Produces(StatusCodes.Status404NotFound)
    .WithSummary("Admin: re-queue a document for indexing");

    group.MapPost("/documents/bulk-delete", async (
      BulkDocumentRequest request,
      ICurrentUser caller,
      ILoggerFactory loggerFactory,
      DeleteDocumentHandler handler,
      CancellationToken ct) =>
    {
      var logger = loggerFactory.CreateLogger(AuditLoggerName);
      int succeeded = 0, failed = 0;

      foreach (var id in request.Ids)
      {
        var result = await handler.HandleAsync(
          new DeleteDocumentCommand(new DocumentId(id), CallerId: null, IsAdmin: true), ct);
        if (result.IsSuccess)
        {
          succeeded++;
          logger.LogWarning("Admin {CallerId} deleted document {DocumentId}", caller.UserId, id);
        }
        else
          failed++;
      }

      return Results.Ok(new BulkOperationResponse(succeeded, failed));
    })
    .Produces<BulkOperationResponse>()
    .WithSummary("Admin: bulk delete documents");

    group.MapPost("/documents/bulk-reindex", async (
      BulkDocumentRequest request,
      ICurrentUser caller,
      ILoggerFactory loggerFactory,
      ReindexDocumentHandler handler,
      CancellationToken ct) =>
    {
      var logger = loggerFactory.CreateLogger(AuditLoggerName);
      int succeeded = 0, failed = 0;

      foreach (var id in request.Ids)
      {
        var result = await handler.HandleAsync(new ReindexDocumentCommand(new DocumentId(id)), ct);
        if (result.IsSuccess)
        {
          succeeded++;
          logger.LogInformation("Admin {CallerId} re-indexed document {DocumentId}", caller.UserId, id);
        }
        else
          failed++;
      }

      return Results.Ok(new BulkOperationResponse(succeeded, failed));
    })
    .Produces<BulkOperationResponse>()
    .WithSummary("Admin: bulk reindex documents");

    group.MapGet("/documents/{id:guid}/preview", async (
      Guid id,
      GetDocumentFileHandler handler,
      IFileStorage storage,
      HttpContext httpContext,
      CancellationToken ct) =>
    {
      return await DocumentFileEndpointHelper.ServeAsync(
        id,
        "inline",
        handler,
        storage,
        callerId: null,
        isAdmin: true,
        httpContext,
        ct);
    })
    .Produces(StatusCodes.Status200OK)
    .Produces(StatusCodes.Status404NotFound)
    .WithSummary("Admin: preview any document");

    group.MapGet("/documents/{id:guid}/download", async (
      Guid id,
      GetDocumentFileHandler handler,
      IFileStorage storage,
      HttpContext httpContext,
      CancellationToken ct) =>
    {
      return await DocumentFileEndpointHelper.ServeAsync(
        id,
        "attachment",
        handler,
        storage,
        callerId: null,
        isAdmin: true,
        httpContext,
        ct);
    })
    .Produces(StatusCodes.Status200OK)
    .Produces(StatusCodes.Status404NotFound)
    .WithSummary("Admin: download any document");

    // ── Users ─────────────────────────────────────────────────────────────────

    group.MapGet("/users", async (
      ListUsersHandler handler,
      CancellationToken ct) =>
    {
      var result = await handler.HandleAsync(new ListUsersQuery(), ct);
      return Results.Ok(result.Value!.Select(u => new
      {
        id          = u.Id,
        email       = u.Email,
        displayName = u.DisplayName,
        isGuest     = u.IsGuest,
        createdAt   = u.CreatedAt,
        roles       = u.Roles,
      }));
    })
    .WithSummary("Admin: list all registered users");

    group.MapDelete("/users/{id}", async (
      string id,
      ICurrentUser caller,
      ILoggerFactory loggerFactory,
      UserManager<ApplicationUser> users,
      CancellationToken ct) =>
    {
      var logger = loggerFactory.CreateLogger(AuditLoggerName);
      var user = await users.FindByIdAsync(id);
      if (user is null) return Results.NotFound();

      var result = await users.DeleteAsync(user);
      if (result.Succeeded)
        logger.LogWarning("Admin {CallerId} deleted user {UserId} ({Email})", caller.UserId, id, user.Email);

      return result.Succeeded ? Results.NoContent() : Results.Problem(
        detail: string.Join("; ", result.Errors.Select(e => e.Description)),
        statusCode: StatusCodes.Status422UnprocessableEntity);
    })
    .Produces(StatusCodes.Status204NoContent)
    .Produces(StatusCodes.Status404NotFound)
    .WithSummary("Admin: delete a user");

    group.MapPost("/users/{id}/reset-password", async (
      string id,
      ResetUserPasswordRequest request,
      ICurrentUser caller,
      ILoggerFactory loggerFactory,
      UserManager<ApplicationUser> users,
      CancellationToken ct) =>
    {
      var logger = loggerFactory.CreateLogger(AuditLoggerName);
      var user = await users.FindByIdAsync(id);
      if (user is null) return Results.NotFound();

      await users.RemovePasswordAsync(user);
      var result = await users.AddPasswordAsync(user, request.NewPassword);

      if (result.Succeeded)
        logger.LogWarning("Admin {CallerId} set password for user {UserId} ({Email})", caller.UserId, id, user.Email);

      return result.Succeeded ? Results.NoContent() : Results.Problem(
        detail: string.Join("; ", result.Errors.Select(e => e.Description)),
        statusCode: StatusCodes.Status422UnprocessableEntity);
    })
    .AddEndpointFilterFactory(ValidationFilter.Create<ResetUserPasswordRequest>())
    .Produces(StatusCodes.Status204NoContent)
    .Produces(StatusCodes.Status404NotFound)
    .WithSummary("Admin: set a new password for any user");

    group.MapPut("/users/{id}/roles", async (
      string id,
      UpdateUserRolesRequest request,
      ICurrentUser caller,
      ILoggerFactory loggerFactory,
      UserManager<ApplicationUser> users,
      CancellationToken ct) =>
    {
      var logger = loggerFactory.CreateLogger(AuditLoggerName);
      var user = await users.FindByIdAsync(id);
      if (user is null) return Results.NotFound();

      var current = await users.GetRolesAsync(user);
      await users.RemoveFromRolesAsync(user, current);
      var addResult = await users.AddToRolesAsync(user, request.Roles);

      if (addResult.Succeeded)
        logger.LogInformation("Admin {CallerId} set roles [{Roles}] on user {UserId}",
          caller.UserId, string.Join(", ", request.Roles), id);

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
      CancellationToken ct) =>
    {
      var result = await handler.HandleAsync(new GetAdminStatsQuery(), ct);
      var dto    = result.Value!;
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
