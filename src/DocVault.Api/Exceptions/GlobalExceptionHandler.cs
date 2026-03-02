using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using DocVault.Api.Errors;
using DocVault.Api.Middleware;
using DocVault.Api.Validation;
using DocVault.Domain.Primitives;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DocVault.Api.Exceptions;

public sealed partial class GlobalExceptionHandler : IExceptionHandler
{
  private readonly ILogger<GlobalExceptionHandler> _logger;
  private readonly IProblemDetailsService _problemDetailsService;

  public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, IProblemDetailsService problemDetailsService)
  {
    _logger = logger;
    _problemDetailsService = problemDetailsService;
  }

  public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
  {
    var descriptor = MapException(exception);

    if (descriptor.StatusCode >= StatusCodes.Status500InternalServerError)
      LogUnhandledException(exception, descriptor.StatusCode, descriptor.ErrorCode, httpContext.Request.Path);
    else
      LogHandledException(descriptor.StatusCode, descriptor.ErrorCode, httpContext.Request.Path, exception.Message);

    var problem = BuildProblemDetails(httpContext, descriptor);
    httpContext.Response.StatusCode = descriptor.StatusCode;
    httpContext.Response.ContentType = "application/problem+json";

    await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
    return true;
  }

  private static ProblemDetails BuildProblemDetails(HttpContext httpContext, ErrorDescriptor descriptor)
  {
    var problem = new ProblemDetails
    {
      Type = $"https://httpstatuses.io/{descriptor.StatusCode}",
      Title = descriptor.Title,
      Status = descriptor.StatusCode,
      Detail = descriptor.Detail,
      Instance = httpContext.Request.Path
    };

    var traceId = Activity.Current?.Id ?? httpContext.TraceIdentifier;
    problem.Extensions["traceId"] = traceId;
    problem.Extensions["errorCode"] = descriptor.ErrorCode;

    if (httpContext.Items.TryGetValue(CorrelationIdMiddleware.HeaderName, out var correlationIdObj)
      && correlationIdObj is string correlationId && !string.IsNullOrWhiteSpace(correlationId))
    {
      problem.Extensions["correlationId"] = correlationId;
    }

    if (descriptor.Extensions is not null)
    {
      foreach (var kvp in descriptor.Extensions)
      {
        problem.Extensions[kvp.Key] = kvp.Value;
      }
    }

    return problem;
  }

  private static ErrorDescriptor MapException(Exception exception)
    => exception switch
    {
      JsonBindingException jsonBinding      => MapJsonBindingException(jsonBinding),
      BadHttpRequestException badRequest    => new ErrorDescriptor(badRequest.StatusCode, "Bad request", ErrorCodes.BadRequest.MALFORMED_BODY, badRequest.Message),
      ValidationException validation        => MapValidationException(validation),
      DomainException domain                => new ErrorDescriptor(StatusCodes.Status400BadRequest, "Domain rule violated", ErrorCodes.BadRequest.DOMAIN_VIOLATION, domain.Message),
      KeyNotFoundException notFound         => new ErrorDescriptor(StatusCodes.Status404NotFound, "Resource not found", ErrorCodes.NotFound.RESOURCE_MISSING, string.IsNullOrWhiteSpace(notFound.Message) ? "The requested resource was not found." : notFound.Message),
      UnauthorizedAccessException           => new ErrorDescriptor(StatusCodes.Status403Forbidden, "Forbidden", ErrorCodes.Forbidden.ACCESS_DENIED, "Access denied."),
      DbUpdateException dbUpdate            => new ErrorDescriptor(StatusCodes.Status503ServiceUnavailable, "Database error", ErrorCodes.ServerError.DATABASE_FAILURE, dbUpdate.InnerException?.Message ?? dbUpdate.Message),
      _                                     => new ErrorDescriptor(StatusCodes.Status500InternalServerError, "Unexpected error", ErrorCodes.ServerError.UNHANDLED, "An unexpected error occurred.")
    };

  private static ErrorDescriptor MapValidationException(ValidationException exception)
  {
    var errors = exception.Errors
      .GroupBy(e => e.PropertyName)
      .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

    return new ErrorDescriptor(
      StatusCodes.Status400BadRequest,
      "Validation failed",
      ErrorCodes.BadRequest.VALIDATION_FAILED,
      "One or more validation errors occurred.",
      new Dictionary<string, object> { ["errors"] = errors }
    );
  }

  private static ErrorDescriptor MapJsonBindingException(JsonBindingException exception)
    => new(
      StatusCodes.Status400BadRequest,
      "Validation failed",
      ErrorCodes.BadRequest.VALIDATION_FAILED,
      "One or more JSON binding errors occurred.",
      new Dictionary<string, object> { ["errors"] = exception.Errors }
    );

  private sealed record ErrorDescriptor(
    int StatusCode,
    string Title,
    string ErrorCode,
    string Detail,
    IDictionary<string, object>? Extensions = null
  );

  [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception => {StatusCode} ({ErrorCode}) Path:{Path}")]
  private partial void LogUnhandledException(Exception exception, int statusCode, string errorCode, string path);

  [LoggerMessage(Level = LogLevel.Warning, Message = "Handled exception => {StatusCode} ({ErrorCode}) Path:{Path}. Message: {Message}")]
  private partial void LogHandledException(int statusCode, string errorCode, string path, string message);
}
