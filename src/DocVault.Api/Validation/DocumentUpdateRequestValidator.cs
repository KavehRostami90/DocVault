using DocVault.Api.Contracts.Documents;
using FluentValidation;

namespace DocVault.Api.Validation;

public sealed class DocumentUpdateRequestValidator : AbstractValidator<DocumentUpdateRequest>
{
  public DocumentUpdateRequestValidator()
  {
    RuleFor(x => x.Title).NotEmpty().MaximumLength(256);
    RuleFor(x => x.Tags).NotNull();
  }
}
