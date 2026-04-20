using System.Text.RegularExpressions;
using DocVault.Api.Contracts.Common;
using DocVault.Api.Contracts.Search;
using DocVault.Api.Middleware;
using DocVault.Api.Validation;
using DocVault.Application.Abstractions.Auth;
using DocVault.Application.UseCases.Search;

namespace DocVault.Api.Endpoints;

public static class SearchEndpoints
{
  public static IEndpointRouteBuilder MapSearchEndpoints(this IEndpointRouteBuilder routes)
  {
    var group = routes.MapGroup("/search")
      .RequireAuthorization();

    group.MapPost("/documents", async (
      SearchRequest request,
      SearchDocumentsHandler handler,
      ICurrentUser currentUser,
      HttpContext httpContext,
      CancellationToken ct) =>
    {
      var result = await handler.HandleAsync(
        new SearchDocumentsQuery(request.Query, request.Page, request.Size,
          OwnerId: currentUser.UserId, IsAdmin: currentUser.IsAdmin), ct);

      if (!result.IsSuccess || result.Value is null)
        return Results.Problem(
          detail: result.Error ?? "Search service temporarily unavailable.",
          statusCode: StatusCodes.Status500InternalServerError);

      var searchResult = result.Value;
      var items = searchResult.Page.Items.Select(ToResponse).ToList();
      httpContext.Response.Headers.Append("X-Search-Mode", searchResult.Mode.ToString().ToLowerInvariant());
      return Results.Ok(new PageResponse<SearchResultItemResponse>(items, request.Page, request.Size, searchResult.Page.TotalCount));
    })
    .RequireRateLimiting(RateLimitPolicies.Search)
    .AddEndpointFilterFactory(ValidationFilter.Create<SearchRequest>())
    .Produces<PageResponse<SearchResultItemResponse>>(StatusCodes.Status200OK)
    .WithSummary("Search documents")
    .WithDescription("Performs full-text search over documents and returns paged results.");

    return routes;
  }

  private static readonly Regex HtmlTagPattern = new("<[^>]*>", RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));

  private static SearchResultItemResponse ToResponse(SearchResultItem item)
  {
    // Prefer the matched chunk text — it's the semantically relevant portion.
    // Fall back to the beginning of the full document text when no chunk is available.
    var raw     = item.MatchedChunkText ?? item.Document.Text ?? string.Empty;
    var plain   = HtmlTagPattern.Replace(raw, string.Empty).Trim();
    var snippet = plain[..Math.Min(plain.Length, 120)];
    return new(item.Document.Id.Value, item.Document.Title, snippet, item.Score);
  }
}
