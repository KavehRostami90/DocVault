using DocVault.Api.Contracts.Common;
using DocVault.Api.Contracts.Documents;
using DocVault.Api.Validation;
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
    var group = routes.MapGroup("/documents");

    group.MapGet("/", async ([AsParameters] ListDocumentsRequest request, ListDocumentsHandler handler, CancellationToken ct) =>
    {
      var result = await handler.HandleAsync(new ListDocumentsQuery(
        request.Page, 
        request.Size, 
        request.Sort, 
        request.Desc, 
        request.Title, 
        request.Status, 
        request.Tag), ct);

      var dto = result.Items.Select(d => new DocumentListItemResponse(d.Id.Value, d.Title, d.FileName, d.Status.ToString())).ToList();
      return Results.Ok(new PageResponse<DocumentListItemResponse>(dto, result.PageNumber, result.PageSize, result.TotalCount));
    })
    .AddEndpointFilterFactory(ValidationFilter.Create<ListDocumentsRequest>())
    .Produces<PageResponse<DocumentListItemResponse>>(StatusCodes.Status200OK)
    .WithSummary("List documents")
    .WithDescription("Returns paged documents with optional sort and filter.");

    group.MapGet("/{id:guid}", async (Guid id, GetDocumentHandler handler, CancellationToken ct) =>
    {
      var outcome = await handler.HandleAsync(new GetDocumentQuery(new DocumentId(id)), ct);
      return outcome.IsSuccess
        ? Results.Ok(ToReadResponse(outcome.Value!))
        : Results.NotFound();
    })
    .Produces<DocumentReadResponse>(StatusCodes.Status200OK)
    .Produces(StatusCodes.Status404NotFound)
    .WithSummary("Get a document")
    .WithDescription("Returns a single document by identifier.");

    group.MapPost("/", async (DocumentCreateRequest request, ImportDocumentHandler handler, CancellationToken ct) =>
    {
      var file = request.File!;
      await using var stream = file.OpenReadStream();

      var result = await handler.HandleAsync(new ImportDocumentCommand(
        request.Title,
        file.FileName,
        file.ContentType,
        file.Length,
        request.Tags,
        stream), ct);

      return result.IsSuccess
        ? Results.Created($"/documents/{result.Value!.Value}", new { id = result.Value!.Value })
        : Results.Problem(detail: result.Error, statusCode: StatusCodes.Status422UnprocessableEntity);
    })
    .DisableAntiforgery()
    .AddEndpointFilterFactory(ValidationFilter.Create<DocumentCreateRequest>())
    .Produces(StatusCodes.Status201Created)
    .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
    .WithSummary("Create (import) a document")
    .WithDescription("Imports a document from multipart/form-data and starts processing.");

    group.MapPut("/{id:guid}/tags", async (Guid id, DocumentUpdateRequest request, UpdateTagsHandler handler, CancellationToken ct) =>
    {
      var result = await handler.HandleAsync(new UpdateTagsCommand(new DocumentId(id), request.Tags), ct);
      return result.IsSuccess ? Results.NoContent() : Results.NotFound();
    })
    .AddEndpointFilterFactory(ValidationFilter.Create<DocumentUpdateRequest>())
    .Produces(StatusCodes.Status204NoContent)
    .Produces(StatusCodes.Status404NotFound)
    .WithSummary("Update document tags")
    .WithDescription("Replaces the tag set for a document.");

    group.MapDelete("/{id:guid}", async (Guid id, DeleteDocumentHandler handler, CancellationToken ct) =>
    {
      var result = await handler.HandleAsync(new DeleteDocumentCommand(new DocumentId(id)), ct);
      return result.IsSuccess ? Results.NoContent() : Results.NotFound();
    })
    .Produces(StatusCodes.Status204NoContent)
    .Produces(StatusCodes.Status404NotFound)
    .WithSummary("Delete a document")
    .WithDescription("Deletes a document by identifier.");

    return routes;
  }

  private static DocumentReadResponse ToReadResponse(Document document)
    => new(document.Id.Value, document.Title, document.FileName, document.ContentType, document.Size, document.Status.ToString(), document.Tags.Select(t => t.Name).ToList());
}
