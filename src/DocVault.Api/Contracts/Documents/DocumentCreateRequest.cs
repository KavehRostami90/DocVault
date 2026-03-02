using System.Reflection;
using DocVault.Api.Validation;

namespace DocVault.Api.Contracts.Documents;

public sealed record DocumentCreateRequest(string Title, string FileName, string ContentType, long Size, IReadOnlyCollection<string> Tags)
  : IBindableFromHttpContext<DocumentCreateRequest>
{
  // Overrides default Minimal API JSON binding to collect ALL type errors at once
  // instead of stopping at the first failure.
  public static ValueTask<DocumentCreateRequest?> BindAsync(HttpContext context, ParameterInfo parameter)
    => JsonValidationBinder.BindAsync<DocumentCreateRequest>(context);
}
