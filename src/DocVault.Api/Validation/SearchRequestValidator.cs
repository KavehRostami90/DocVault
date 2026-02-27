using DocVault.Api.Contracts.Search;
using FluentValidation;

namespace DocVault.Api.Validation;

public sealed class SearchRequestValidator : AbstractValidator<SearchRequest>
{
  public SearchRequestValidator()
  {
    RuleFor(x => x.Query).NotEmpty().MaximumLength(512);
    RuleFor(x => x.Page).GreaterThan(0);
    RuleFor(x => x.Size).InclusiveBetween(1, 200);
  }
}
