using DocVault.Api.Contracts.Search;
using DocVault.Domain.Common;
using FluentValidation;

namespace DocVault.Api.Validation;

public sealed class SearchRequestValidator : AbstractValidator<SearchRequest>
{
  public SearchRequestValidator()
  {
    RuleFor(x => x.Query)
      .NotEmpty()
      .WithMessage("Search query cannot be empty")
      .MinimumLength(ValidationConstants.Search.MIN_QUERY_LENGTH)
      .WithMessage($"Search query must be at least {ValidationConstants.Search.MIN_QUERY_LENGTH} characters")
      .MaximumLength(ValidationConstants.Search.MAX_QUERY_LENGTH)
      .WithMessage($"Search query cannot exceed {ValidationConstants.Search.MAX_QUERY_LENGTH} characters")
      .Must(query => !string.IsNullOrWhiteSpace(query?.Trim()))
      .WithMessage("Search query cannot be only whitespace");

    RuleFor(x => x.Page)
      .GreaterThanOrEqualTo(ValidationConstants.Search.MIN_PAGE)
      .WithMessage($"Page must be at least {ValidationConstants.Search.MIN_PAGE}");

    RuleFor(x => x.Size)
      .InclusiveBetween(ValidationConstants.Search.MIN_PAGE_SIZE, ValidationConstants.Search.MAX_PAGE_SIZE)
      .WithMessage($"Page size must be between {ValidationConstants.Search.MIN_PAGE_SIZE} and {ValidationConstants.Search.MAX_PAGE_SIZE}");
  }
}
