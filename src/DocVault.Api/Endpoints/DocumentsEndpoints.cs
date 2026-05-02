using DocVault.Api.Contracts.Common;
using DocVault.Api.Contracts.Documents;
using DocVault.Api.Validation;
using DocVault.Api.Mappers;
using DocVault.Api.Middleware;
using DocVault.Application.Abstractions.Auth;
using DocVault.Application.Abstractions.Realtime;
using DocVault.Application.Abstractions.Storage;
using DocVault.Application.UseCases.Documents.DeleteDocument;
using DocVault.Application.UseCases.Documents.GetDocument;
using DocVault.Application.UseCases.Documents.GetDocumentFile;
using DocVault.Application.UseCases.Documents.ImportDocument;
using DocVault.Application.UseCases.Documents.ListDocuments;
using DocVault.Application.UseCases.Documents.UpdateTags;
using DocVault.Domain.Documents;
using Microsoft.Net.Http.Headers;
using System.Text.Json;

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

    group.MapGet("/{id:guid}/preview", async (
      Guid id,
      GetDocumentFileHandler handler,
      IFileStorage storage,
      ICurrentUser currentUser,
      HttpContext httpContext,
      CancellationToken ct) =>
    {
      return await DocumentFileEndpointHelper.ServeAsync(
        id,
        "inline",
        handler,
        storage,
        currentUser.UserId,
        currentUser.IsAdmin,
        httpContext,
        ct);
    })
    .Produces(StatusCodes.Status200OK)
    .Produces(StatusCodes.Status404NotFound)
    .WithSummary("Preview a document")
    .WithDescription("Streams the original file inline when the browser supports the content type.");

    group.MapGet("/{id:guid}/download", async (
      Guid id,
      GetDocumentFileHandler handler,
      IFileStorage storage,
      ICurrentUser currentUser,
      HttpContext httpContext,
      CancellationToken ct) =>
    {
      return await DocumentFileEndpointHelper.ServeAsync(
        id,
        "attachment",
        handler,
        storage,
        currentUser.UserId,
        currentUser.IsAdmin,
        httpContext,
        ct);
    })
    .Produces(StatusCodes.Status200OK)
    .Produces(StatusCodes.Status404NotFound)
    .WithSummary("Download a document")
    .WithDescription("Downloads the original stored file for a document.");

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

    group.MapGet("/{id:guid}/status-stream", async (
      Guid id,
      IDocumentStatusBroadcaster broadcaster,
      GetDocumentHandler handler,
      ICurrentUser currentUser,
      HttpResponse response,
      CancellationToken ct) =>
    {
      var reader = broadcaster.Subscribe(id);
      try
      {
        var outcome = await handler.HandleAsync(
          new GetDocumentQuery(new DocumentId(id), currentUser.UserId, currentUser.IsAdmin), ct);

        if (!outcome.IsSuccess || outcome.Value is null)
        {
          response.StatusCode = StatusCodes.Status404NotFound;
          return;
        }

        var doc = outcome.Value;
        response.Headers.ContentType = "text/event-stream";
        response.Headers.CacheControl = "no-cache";
        response.Headers.Append("X-Accel-Buffering", "no");

        if (doc.Status is DocumentStatus.Indexed or DocumentStatus.Failed)
        {
          await WriteSseEventAsync(response, doc.Id.Value, doc.Status, doc.IndexingError, ct);
          return;
        }

        await foreach (var evt in reader.ReadAllAsync(ct))
        {
          await WriteSseEventAsync(response, evt.DocumentId, evt.Status, evt.Error, ct);
          if (evt.Status is DocumentStatus.Indexed or DocumentStatus.Failed) break;
        }
      }
      catch (OperationCanceledException) { }
      finally
      {
        broadcaster.Unsubscribe(id, reader);
      }
    })
    .Produces(StatusCodes.Status200OK)
    .Produces(StatusCodes.Status404NotFound)
    .WithSummary("Stream document status")
    .WithDescription("SSE stream that emits a status event when the document transitions to Indexed or Failed. Completes immediately if the document is already in a terminal state.");

    group.MapGet("/{id:guid}/extracted-text", async (
      Guid id,
      bool download,
      GetDocumentHandler handler,
      ICurrentUser currentUser,
      HttpContext httpContext,
      CancellationToken ct) =>
    {
      var outcome = await handler.HandleAsync(
        new GetDocumentQuery(new DocumentId(id), currentUser.UserId, currentUser.IsAdmin), ct);

      if (!outcome.IsSuccess)
        return Results.NotFound();

      var doc = outcome.Value!;
      var text = doc.Text ?? string.Empty;
      var textFileName = Path.GetFileNameWithoutExtension(doc.FileName) + ".txt";
      var dispositionType = download ? "attachment" : "inline";

      httpContext.Response.Headers.Append(
        HeaderNames.ContentDisposition,
        new ContentDispositionHeaderValue(dispositionType)
        {
          FileName = textFileName,
          FileNameStar = textFileName
        }.ToString());

      return Results.Content(text, "text/plain; charset=utf-8");
    })
    .Produces(StatusCodes.Status200OK)
    .Produces(StatusCodes.Status404NotFound)
    .WithSummary("Get extracted text")
    .WithDescription("Returns the OCR-extracted or parsed text content of a document as plain text. Pass ?download=true to receive a .txt file attachment.");

    return routes;
  }

  private static async Task WriteSseEventAsync(
    HttpResponse response, Guid documentId, DocumentStatus status, string? error, CancellationToken ct)
  {
    var json = JsonSerializer.Serialize(new { documentId, status = status.ToString(), error });
    await response.WriteAsync($"data: {json}\n\n", ct);
    await response.Body.FlushAsync(ct);
  }
}
