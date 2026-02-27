using DocVault.Domain.Primitives;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DocVault.Api.Exceptions;

public sealed class GlobalExceptionHandler : IExceptionHandler
{
  private readonly ILogger<GlobalExceptionHandler> _logger;

  public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
  {
    _logger = logger;
  }

  public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
  {
    (int statusCode, string? title, string? detail) = MapException(exception);
    _logger.LogError(exception, "Unhandled exception returned {StatusCode}", statusCode);

    httpContext.Response.StatusCode = statusCode;
    var problem = new ProblemDetails
    {
      Status = statusCode,
      Title = title,
      Detail = detail,
      Instance = httpContext.Request.Path
    };

    await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
    return true;
  }

  private static (int StatusCode, string Title, string Detail) MapException(Exception exception)
    => exception switch
    {
      ValidationException ve => (StatusCodes.Status400BadRequest, "Validation failed", ve.Message),
      DomainException de => (StatusCodes.Status422UnprocessableEntity, "Domain rule violated", de.Message),
      KeyNotFoundException knf => (StatusCodes.Status404NotFound, "Resource not found", knf.Message),
      DbUpdateException db => (StatusCodes.Status500InternalServerError, "Database error", db.InnerException?.Message ?? db.Message),
      _ => (StatusCodes.Status500InternalServerError, "Unexpected error", "An unexpected error occurred.")
    };
}
