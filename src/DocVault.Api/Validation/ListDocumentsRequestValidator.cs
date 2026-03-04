using DocVault.Api.Contracts.Documents;
using DocVault.Domain.Common;
using FluentValidation;

namespace DocVault.Api.Validation;

public sealed class ListDocumentsRequestValidator : AbstractValidator<ListDocumentsRequest>
{
  public ListDocumentsRequestValidator()
  {
    RuleFor(x => x.Page)
      .GreaterThanOrEqualTo(ValidationConstants.Paging.DEFAULT_PAGE)
      .WithMessage($"Page must be at least {ValidationConstants.Paging.DEFAULT_PAGE}");

    RuleFor(x => x.Size)
      .InclusiveBetween(ValidationConstants.Search.MIN_PAGE_SIZE, ValidationConstants.Search.MAX_PAGE_SIZE)
      .WithMessage($"Size must be between {ValidationConstants.Search.MIN_PAGE_SIZE} and {ValidationConstants.Search.MAX_PAGE_SIZE}");

    RuleFor(x => x.Sort)
      .Must(sort => string.IsNullOrEmpty(sort) || ValidationConstants.Paging.VALID_DOCUMENT_SORT_FIELDS.Contains(sort.ToLowerInvariant()))
      .WithMessage($"Sort field must be one of: {string.Join(", ", ValidationConstants.Paging.VALID_DOCUMENT_SORT_FIELDS)}");

    RuleFor(x => x.Title)
      .MaximumLength(ValidationConstants.Paging.MAX_FILTER_LENGTH)
      .WithMessage($"Title filter cannot exceed {ValidationConstants.Paging.MAX_FILTER_LENGTH} characters");

    RuleFor(x => x.Status)
      .Must(status => string.IsNullOrEmpty(status) || ValidationConstants.Paging.VALID_DOCUMENT_STATUSES.Contains(status.ToLowerInvariant()))
      .WithMessage($"Status must be one of: {string.Join(", ", ValidationConstants.Paging.VALID_DOCUMENT_STATUSES)}");

    RuleFor(x => x.Tag)
      .MaximumLength(ValidationConstants.Paging.MAX_FILTER_LENGTH)
      .WithMessage($"Tag filter cannot exceed {ValidationConstants.Paging.MAX_FILTER_LENGTH} characters");
  }
}
