using System.Text;
using System.Text.Json;
using DocVault.Api.Contracts.Qa;
using DocVault.Api.Middleware;
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
    .RequireRateLimiting(RateLimitPolicies.Qa)
    .AddEndpointFilterFactory(ValidationFilter.Create<AskQuestionRequest>())
    .Produces<AskQuestionResponse>(StatusCodes.Status200OK)
    .WithSummary("Ask a question over documents")
    .WithDescription("Runs retrieval over indexed document content and returns an answer with citations.");

    // SSE streaming variant — emits token deltas as they arrive from the LLM.
    group.MapPost("/ask/stream", async (
      AskQuestionRequest request,
      AskQuestionHandler handler,
      ICurrentUser currentUser,
      HttpContext httpContext,
      CancellationToken ct) =>
    {
      httpContext.Response.ContentType  = "text/event-stream; charset=utf-8";
      httpContext.Response.Headers.CacheControl = "no-cache";
      httpContext.Response.Headers["X-Accel-Buffering"] = "no"; // disable nginx / reverse-proxy buffering

      try
      {
        await foreach (var token in handler.HandleStreamAsync(
          new AskQuestionQuery(
            request.Question,
            request.MaxDocuments,
            request.MaxContexts,
            request.DocumentId,
            OwnerId: currentUser.UserId,
            IsAdmin: currentUser.IsAdmin),
          ct))
        {
          var line  = $"data: {JsonSerializer.Serialize(token)}\n\n";
          var bytes = Encoding.UTF8.GetBytes(line);
          await httpContext.Response.Body.WriteAsync(bytes, ct);
          await httpContext.Response.Body.FlushAsync(ct);
        }

        await httpContext.Response.Body.WriteAsync("data: [DONE]\n\n"u8.ToArray(), ct);
        await httpContext.Response.Body.FlushAsync(ct);
      }
      catch (OperationCanceledException) { /* client disconnected */ }
    })
    .RequireRateLimiting(RateLimitPolicies.Qa)
    .AddEndpointFilterFactory(ValidationFilter.Create<AskQuestionRequest>())
    .WithSummary("Ask a question (streaming)")
    .WithDescription("Streams the LLM answer token-by-token using Server-Sent Events. " +
                     "Each event carries a JSON-encoded token delta; the final event is 'data: [DONE]'.");

    return routes;
  }
}
