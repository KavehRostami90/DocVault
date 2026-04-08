using DocVault.Api.Contracts.Common;
using DocVault.Api.Contracts.Documents;
using DocVault.Api.Validation;
using DocVault.Api.Mappers;
using DocVault.Api.Middleware;
using DocVault.Application.Abstractions.Auth;
using DocVault.Application.Common.Paging;
using DocVault.Application.UseCases.Documents.DeleteDocument;
using DocVault.Application.UseCases.Documents.GetDocument;
using DocVault.Application.UseCases.Documents.ImportDocument;
using DocVault.Application.UseCases.Documents.ListDocuments;
using DocVault.Application.UseCases.Documents.UpdateTags;
using DocVault.Domain.Documents;

namespace DocVault.Api.Endpoints;

public static class DocumentsEndpoints
{
  public static IEndpointRouteBuilder MapDocumentsEndpoints(this IEndpointRouteBuilder routes)
  {
    var group = routes.MapGroup("/documents")
      .RequireAuthorization();

    group.MapGet("/", async (
      [AsParameters] ListDocumentsRequest request,
      ListDocumentsHandler handler,
      ICurrentUser currentUser,
      CancellationToken ct) =>
    {
      var result = await handler.HandleAsync(new ListDocumentsQuery(
        request.Page, request.Size, request.Sort, request.Desc,
        request.Title, request.Status, request.Tag,
        OwnerId: currentUser.UserId, IsAdmin: currentUser.IsAdmin), ct);

      return Results.Ok(DocumentResponseMapper.ToPage(result));
    })
    .AddEndpointFilterFactory(ValidationFilter.Create<ListDocumentsRequest>())
    .Produces<PageResponse<DocumentListItemResponse>>(StatusCodes.Status200OK)
    .WithSummary("List documents")
    .WithDescription("Returns paged documents with optional sort and filter.");

    group.MapGet("/{id:guid}", async (
      Guid id,
      GetDocumentHandler handler,
      ICurrentUser currentUser,
      CancellationToken ct) =>
    {
      var outcome = await handler.HandleAsync(
        new GetDocumentQuery(new DocumentId(id), currentUser.UserId, currentUser.IsAdmin), ct);
      return outcome.IsSuccess
        ? Results.Ok(DocumentResponseMapper.ToRead(outcome.Value!))
        : Results.NotFound();
    })
    .Produces<DocumentReadResponse>(StatusCodes.Status200OK)
    .Produces(StatusCodes.Status404NotFound)
    .WithSummary("Get a document")
    .WithDescription("Returns a single document by identifier.");

    group.MapPost("/", async (
      DocumentCreateRequest request,
      ImportDocumentHandler handler,
      ICurrentUser currentUser,
      CancellationToken ct) =>
    {
      var file = request.File;
      if (file is null)
        return Results.Problem(detail: "No file was uploaded.", statusCode: StatusCodes.Status400BadRequest);

      await using var stream = file.OpenReadStream();

      var result = await handler.HandleAsync(new ImportDocumentCommand(
        request.Title,
        file.FileName,
        file.ContentType,
        file.Length,
        request.Tags,
        stream,
        OwnerId: currentUser.UserId), ct);

      return result.IsSuccess
        ? Results.Created($"/documents/{result.Value!.Value}", new { id = result.Value!.Value })
        : Results.Problem(detail: result.Error, statusCode: StatusCodes.Status422UnprocessableEntity);
    })
    .DisableAntiforgery()
    .RequireRateLimiting(RateLimitPolicies.DocumentUpload)
    .AddEndpointFilterFactory(ValidationFilter.Create<DocumentCreateRequest>())
    .Produces(StatusCodes.Status201Created)
    .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
    .WithSummary("Create (import) a document")
    .WithDescription("Imports a document from multipart/form-data and starts processing.");

    group.MapPut("/{id:guid}/tags", async (
      Guid id,
      DocumentUpdateRequest request,
      UpdateTagsHandler handler,
      ICurrentUser currentUser,
      CancellationToken ct) =>
    {
      var result = await handler.HandleAsync(
        new UpdateTagsCommand(new DocumentId(id), request.Tags, currentUser.UserId, currentUser.IsAdmin), ct);
      return result.IsSuccess ? Results.NoContent() : Results.NotFound();
    })
    .AddEndpointFilterFactory(ValidationFilter.Create<DocumentUpdateRequest>())
    .Produces(StatusCodes.Status204NoContent)
    .Produces(StatusCodes.Status404NotFound)
    .WithSummary("Update document tags")
    .WithDescription("Replaces the tag set for a document.");

    group.MapDelete("/{id:guid}", async (
      Guid id,
      DeleteDocumentHandler handler,
      ICurrentUser currentUser,
      CancellationToken ct) =>
    {
      var result = await handler.HandleAsync(
        new DeleteDocumentCommand(new DocumentId(id), currentUser.UserId, currentUser.IsAdmin), ct);
      return result.IsSuccess ? Results.NoContent() : Results.NotFound();
    })
    .Produces(StatusCodes.Status204NoContent)
    .Produces(StatusCodes.Status404NotFound)
    .WithSummary("Delete a document")
    .WithDescription("Deletes a document by identifier.");

    return routes;
  }
}
