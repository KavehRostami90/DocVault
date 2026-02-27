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

    group.MapGet("/", async (int page, int size, string? sort, bool desc, string? title, string? status, string? tag, ListDocumentsHandler handler, CancellationToken ct) =>
    {
      var result = await handler.HandleAsync(new ListDocumentsQuery(page, size, sort, desc, title, status, tag), ct);
      var dto = result.Items.Select(d => new DocumentListItemResponse(d.Id.Value, d.Title, d.FileName, d.Status.ToString())).ToList();
      return Results.Ok(new PageResponse<DocumentListItemResponse>(dto, result.PageNumber, result.PageSize, result.TotalCount));
    });

    group.MapGet("/{id:guid}", async (Guid id, GetDocumentHandler handler, CancellationToken ct) =>
    {
      var outcome = await handler.HandleAsync(new GetDocumentQuery(new DocumentId(id)), ct);
      return outcome.IsSuccess
        ? Results.Ok(ToReadResponse(outcome.Value!))
        : Results.NotFound();
    });

    group.MapPost("/", async (DocumentCreateRequest request, ImportDocumentHandler handler, CancellationToken ct) =>
    {
      await using var stream = new MemoryStream();
      var result = await handler.HandleAsync(new ImportDocumentCommand(request.FileName, stream), ct);
      return result.IsSuccess
        ? Results.Created($"/documents/{result.Value!.Value}", new { id = result.Value!.Value })
        : Results.BadRequest(new { error = result.Error });
    })
    .AddEndpointFilterFactory(ValidationFilter.Create<DocumentCreateRequest>());

    group.MapPut("/{id:guid}/tags", async (Guid id, DocumentUpdateRequest request, UpdateTagsHandler handler, CancellationToken ct) =>
    {
      var result = await handler.HandleAsync(new UpdateTagsCommand(new DocumentId(id), request.Tags), ct);
      return result.IsSuccess ? Results.NoContent() : Results.NotFound();
    })
    .AddEndpointFilterFactory(ValidationFilter.Create<DocumentUpdateRequest>());

    group.MapDelete("/{id:guid}", async (Guid id, DeleteDocumentHandler handler, CancellationToken ct) =>
    {
      var result = await handler.HandleAsync(new DeleteDocumentCommand(new DocumentId(id)), ct);
      return result.IsSuccess ? Results.NoContent() : Results.NotFound();
    });

    return routes;
  }

  private static DocumentReadResponse ToReadResponse(Document document)
    => new(document.Id.Value, document.Title, document.FileName, document.ContentType, document.Size, document.Status.ToString(), document.Tags.Select(t => t.Name).ToList());
}
