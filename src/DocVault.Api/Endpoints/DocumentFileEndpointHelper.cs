using DocVault.Application.Abstractions.Storage;
using DocVault.Application.UseCases.Documents.GetDocumentFile;
using DocVault.Domain.Documents;
using Microsoft.Net.Http.Headers;

namespace DocVault.Api.Endpoints;

internal static class DocumentFileEndpointHelper
{
  public static async Task<IResult> ServeAsync(
    Guid documentId,
    string dispositionType,
    GetDocumentFileHandler handler,
    IFileStorage storage,
    Guid? callerId,
    bool isAdmin,
    HttpContext httpContext,
    CancellationToken ct)
  {
    var outcome = await handler.HandleAsync(
      new GetDocumentFileQuery(new DocumentId(documentId), callerId, isAdmin),
      ct);

    if (!outcome.IsSuccess)
      return Results.NotFound();

    try
    {
      var file = outcome.Value!;
      httpContext.Response.Headers.Append(
        HeaderNames.ContentDisposition,
        CreateContentDisposition(dispositionType, file.FileName));

      var stream = await storage.ReadAsync(file.StoragePath, ct);
      return Results.Stream(stream, file.ContentType, enableRangeProcessing: true);
    }
    catch (FileNotFoundException)
    {
      return Results.NotFound();
    }
  }

  private static string CreateContentDisposition(string dispositionType, string fileName)
  {
    var headerValue = new ContentDispositionHeaderValue(dispositionType)
    {
      FileName = fileName,
      FileNameStar = fileName
    };

    return headerValue.ToString();
  }
}
