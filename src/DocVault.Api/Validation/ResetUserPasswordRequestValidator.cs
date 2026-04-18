using DocVault.Api.Contracts.Admin;
using DocVault.Domain.Common;
using FluentValidation;

namespace DocVault.Api.Validation;

public sealed class ResetUserPasswordRequestValidator : AbstractValidator<ResetUserPasswordRequest>
{
  public ResetUserPasswordRequestValidator()
  {
    RuleFor(x => x.NewPassword)
      .NotEmpty()
      .MinimumLength(ValidationConstants.Auth.MIN_PASSWORD_LENGTH)
      .MaximumLength(ValidationConstants.Auth.MAX_PASSWORD_LENGTH);
  }
}
