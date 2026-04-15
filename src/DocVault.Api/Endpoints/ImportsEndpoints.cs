using DocVault.Api.Contracts.Imports;
using DocVault.Api.Validation;
using DocVault.Application.Abstractions.Auth;
using DocVault.Application.UseCases.Imports.GetImportStatus;
using DocVault.Application.UseCases.Imports.StartImportJob;

namespace DocVault.Api.Endpoints;

/// <summary>
/// Maps import endpoints.
/// </summary>
public static class ImportsEndpoints
{
  public static IEndpointRouteBuilder MapImportsEndpoints(this IEndpointRouteBuilder routes)
  {
    var group = routes.MapGroup("/imports")
      .RequireAuthorization();

    group.MapPost("/", async (ImportCreateRequest request, StartImportJobHandler handler, ICurrentUser currentUser, CancellationToken ct) =>
    {
      var command = new StartImportJobCommand(
        new DocVault.Domain.Documents.DocumentId(request.DocumentId),
        request.FileName,
        request.StoragePath,
        request.ContentType,
        CallerId: currentUser.UserId,
        IsAdmin:  currentUser.IsAdmin);
      var result = await handler.HandleAsync(command, ct);
      return result.IsSuccess ? Results.Accepted($"/imports/{result.Value}", new { id = result.Value }) : Results.NotFound();
    })
    .AddEndpointFilterFactory(ValidationFilter.Create<ImportCreateRequest>())
    .Produces(StatusCodes.Status202Accepted)
    .Produces(StatusCodes.Status404NotFound)
    .WithSummary("Start an import job")
    .WithDescription("Starts an import job for a previously uploaded file name.");

    group.MapGet("/{id:guid}", async (Guid id, GetImportStatusHandler handler, ICurrentUser currentUser, CancellationToken ct) =>
    {
      var result = await handler.HandleAsync(new GetImportStatusQuery(id, currentUser.UserId, currentUser.IsAdmin), ct);
      if (!result.IsSuccess || result.Value is null)
        return Results.NotFound();

      var job = result.Value;
      // Sanitize error: only expose a generic message — raw exception details stay in logs.
      var safeError = job.Error is not null ? "The import job failed during processing." : null;
      return Results.Ok(new ImportStatusResponse(job.Id, job.FileName, job.Status.ToString(), job.StartedAt, job.CompletedAt, safeError));
    })
    .Produces<ImportStatusResponse>(StatusCodes.Status200OK)
    .Produces(StatusCodes.Status404NotFound)
    .WithSummary("Get import status")
    .WithDescription("Returns the status of an import job by identifier.");

    return routes;
  }
}
