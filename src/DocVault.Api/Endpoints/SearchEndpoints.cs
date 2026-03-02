using DocVault.Api.Contracts.Common;
using DocVault.Api.Contracts.Search;
using DocVault.Api.Validation;
using DocVault.Application.UseCases.Search;

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

      var page  = result.Value;
      var items = page.Items.Select(ToResponse).ToList();
      return Results.Ok(new PageResponse<SearchResultItemResponse>(items, request.Page, request.Size, page.TotalCount));
    })
    .AddEndpointFilterFactory(ValidationFilter.Create<SearchRequest>());

    return routes;
  }

  private static SearchResultItemResponse ToResponse(SearchResultItem item)
    => new(item.Document.Id.Value, item.Document.Title, item.Document.Text[..Math.Min(item.Document.Text.Length, 120)], item.Score);
}
