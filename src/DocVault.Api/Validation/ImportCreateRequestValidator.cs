using DocVault.Api.Contracts.Imports;
using FluentValidation;

namespace DocVault.Api.Validation;

public sealed class ImportCreateRequestValidator : AbstractValidator<ImportCreateRequest>
{
  public ImportCreateRequestValidator()
  {
    RuleFor(x => x.FileName).NotEmpty().MaximumLength(256).WithMessage("FileName is required and must not exceed 256 characters.");
  }
}
