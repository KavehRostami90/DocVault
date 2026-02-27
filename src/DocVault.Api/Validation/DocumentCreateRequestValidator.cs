using DocVault.Api.Contracts.Documents;
using FluentValidation;

namespace DocVault.Api.Validation;

public sealed class DocumentCreateRequestValidator : AbstractValidator<DocumentCreateRequest>
{
  public DocumentCreateRequestValidator()
  {
    RuleFor(x => x.Title).NotEmpty().MaximumLength(256);
    RuleFor(x => x.FileName).NotEmpty().MaximumLength(256);
    RuleFor(x => x.ContentType).NotEmpty().MaximumLength(128);
    RuleFor(x => x.Size).GreaterThanOrEqualTo(0);
    RuleFor(x => x.Tags).NotNull();
  }
}
