using DocVault.Api.Contracts.Common;
using DocVault.Api.Contracts.Search;
using DocVault.Api.Validation;
using DocVault.Application.UseCases.Search;
using DocVault.Domain.Documents;

namespace DocVault.Api.Endpoints;

public static class SearchEndpoints
{
  public static IEndpointRouteBuilder MapSearchEndpoints(this IEndpointRouteBuilder routes)
  {
    var group = routes.MapGroup("/search");

    group.MapPost("/documents", async (SearchRequest request, SearchDocumentsHandler handler, CancellationToken ct) =>
    {
      var result = await handler.HandleAsync(new SearchDocumentsQuery(request.Query, request.Page, request.Size), ct);
      if (!result.IsSuccess || result.Value is null)
      {
        return Results.Ok(new PageResponse<SearchResultItemResponse>(Array.Empty<SearchResultItemResponse>(), request.Page, request.Size, 0));
      }

      var items = result.Value.Select(ToResponse).ToList();
      return Results.Ok(new PageResponse<SearchResultItemResponse>(items, request.Page, request.Size, items.Count));
    })
    .AddEndpointFilterFactory(ValidationFilter.Create<SearchRequest>());

    return routes;
  }

  private static SearchResultItemResponse ToResponse(Document doc)
    => new(doc.Id.Value, doc.Title, doc.Text[..Math.Min(doc.Text.Length, 120)], 0);
}
