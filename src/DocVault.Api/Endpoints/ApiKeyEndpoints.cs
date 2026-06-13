using DocVault.Api.Composition;
using DocVault.Application.Abstractions.Auth;
using DocVault.Application.UseCases.ApiKeys.CreateApiKey;
using DocVault.Application.UseCases.ApiKeys.ListApiKeys;
using DocVault.Application.UseCases.ApiKeys.RevokeApiKey;

namespace DocVault.Api.Endpoints;

/// <summary>
/// API key management endpoints.
/// Regular users manage their own keys under <c>/api-keys</c>.
/// Admins can manage any key under <c>/admin/api-keys</c>.
/// </summary>
public static class ApiKeyEndpoints
{
  public static IEndpointRouteBuilder MapApiKeyEndpoints(this IEndpointRouteBuilder routes)
  {
    // ── User-facing (/api-keys) ─────────────────────────────────────────────
    var user = routes.MapGroup("/api-keys")
      .RequireAuthorization();

    user.MapGet("/", async (
      ICurrentUser caller,
      ListApiKeysHandler handler,
      CancellationToken ct) =>
    {
      if (caller.UserId is null) return Results.Unauthorized();
      var result = await handler.HandleAsync(new ListApiKeysQuery(caller.UserId.ToString()!), ct);
      return Results.Ok(result.Value!);
    })
    .Produces<IReadOnlyList<ApiKeyDto>>()
    .WithSummary("List your API keys");

    user.MapPost("/", async (
      CreateApiKeyRequest request,
      ICurrentUser caller,
      CreateApiKeyHandler handler,
      CancellationToken ct) =>
    {
      if (caller.UserId is null) return Results.Unauthorized();
      var result = await handler.HandleAsync(
        new CreateApiKeyCommand(caller.UserId.ToString()!, request.Name, request.ExpiresAt), ct);

      return result.IsSuccess
        ? Results.Created($"/api/v1/api-keys/{result.Value!.Id}", result.Value)
        : Results.Problem(result.Error, statusCode: StatusCodes.Status422UnprocessableEntity);
    })
    .Produces<CreateApiKeyResult>(StatusCodes.Status201Created)
    .WithSummary("Create a new API key — the key is shown only once");

    user.MapDelete("/{id:guid}", async (
      Guid id,
      ICurrentUser caller,
      RevokeApiKeyHandler handler,
      CancellationToken ct) =>
    {
      if (caller.UserId is null) return Results.Unauthorized();
      var result = await handler.HandleAsync(
        new RevokeApiKeyCommand(id, caller.UserId.ToString()!), ct);

      return result.IsSuccess ? Results.NoContent()
        : result.ErrorCode == "NOT_FOUND" ? Results.NotFound()
        : Results.Problem(result.Error, statusCode: StatusCodes.Status422UnprocessableEntity);
    })
    .Produces(StatusCodes.Status204NoContent)
    .Produces(StatusCodes.Status404NotFound)
    .WithSummary("Revoke one of your API keys");

    // ── Admin-facing (/admin/api-keys) ──────────────────────────────────────
    var admin = routes.MapGroup("/admin/api-keys")
      .RequireAuthorization(AuthPolicies.RequireAdmin);

    admin.MapGet("/", async (
      string? userId,
      ListApiKeysHandler handler,
      CancellationToken ct) =>
    {
      if (string.IsNullOrWhiteSpace(userId))
        return Results.BadRequest("userId query parameter is required.");
      var result = await handler.HandleAsync(new ListApiKeysQuery(userId), ct);
      return Results.Ok(result.Value!);
    })
    .Produces<IReadOnlyList<ApiKeyDto>>()
    .WithSummary("Admin: list API keys for a given user");

    admin.MapDelete("/{id:guid}", async (
      Guid id,
      ICurrentUser caller,
      RevokeApiKeyHandler handler,
      CancellationToken ct) =>
    {
      var result = await handler.HandleAsync(
        new RevokeApiKeyCommand(id, caller.UserId?.ToString() ?? string.Empty, IsAdmin: true), ct);

      return result.IsSuccess ? Results.NoContent()
        : result.ErrorCode == "NOT_FOUND" ? Results.NotFound()
        : Results.Problem(result.Error, statusCode: StatusCodes.Status422UnprocessableEntity);
    })
    .Produces(StatusCodes.Status204NoContent)
    .Produces(StatusCodes.Status404NotFound)
    .WithSummary("Admin: revoke any API key");

    return routes;
  }

  private sealed record CreateApiKeyRequest(string Name, DateTimeOffset? ExpiresAt);
}
