using FluentValidation;
using Microsoft.Extensions.Logging;

namespace DocVault.Api.Validation;

public static class ValidationFilter
{
  public static Func<EndpointFilterFactoryContext, EndpointFilterDelegate, EndpointFilterDelegate> Create<TRequest>()
    => (context, next) =>
    {
      return async invocationContext =>
      {
        var validator = invocationContext.HttpContext.RequestServices.GetService<IValidator<TRequest>>();
        if (validator is not null)
        {
          var model = invocationContext.Arguments.OfType<TRequest>().FirstOrDefault();
          if (model is not null)
          {
            var result = await validator.ValidateAsync(model, invocationContext.HttpContext.RequestAborted);
            if (!result.IsValid)
            {
              var errors = result.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
              var logger = invocationContext.HttpContext.RequestServices.GetService<ILoggerFactory>()?.CreateLogger("Validation");
              logger?.LogWarning("Validation failed for {Path}: {Errors}", invocationContext.HttpContext.Request.Path, errors);
              return Results.ValidationProblem(errors, statusCode: StatusCodes.Status400BadRequest);
            }
          }
        }
        return await next(invocationContext);
      };
    };
}
