using DocVault.Api.Contracts.Qa;
using DocVault.Api.Validation;
using DocVault.Application.Abstractions.Auth;
using DocVault.Application.UseCases.Qa;

namespace DocVault.Api.Endpoints;

public static class QaEndpoints
{
  public static IEndpointRouteBuilder MapQaEndpoints(this IEndpointRouteBuilder routes)
  {
    var group = routes.MapGroup("/qa")
      .RequireAuthorization();

    group.MapPost("/ask", async (
      AskQuestionRequest request,
      AskQuestionHandler handler,
      ICurrentUser currentUser,
      CancellationToken ct) =>
    {
      var result = await handler.HandleAsync(
        new AskQuestionQuery(
          request.Question,
          request.MaxDocuments,
          request.MaxContexts,
          request.DocumentId,
          OwnerId: currentUser.UserId,
          IsAdmin: currentUser.IsAdmin),
        ct);

      if (!result.IsSuccess || result.Value is null)
        return Results.Problem(
          detail: result.Error ?? "Question-answering service temporarily unavailable.",
          statusCode: StatusCodes.Status503ServiceUnavailable);

      var payload = result.Value;
      var response = new AskQuestionResponse(
        payload.Answer,
        payload.AnsweredByModel,
        payload.Citations.Select(c => new AskQuestionCitationResponse(c.DocumentId, c.Title, c.Excerpt, c.Score)).ToList());

      return Results.Ok(response);
    })
    .AddEndpointFilterFactory(ValidationFilter.Create<AskQuestionRequest>())
    .Produces<AskQuestionResponse>(StatusCodes.Status200OK)
    .WithSummary("Ask a question over documents")
    .WithDescription("Runs retrieval over indexed document content and returns an answer with citations.");

    return routes;
  }
}
