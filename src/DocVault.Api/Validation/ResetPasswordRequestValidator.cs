using DocVault.Api.Contracts.Auth;
using DocVault.Domain.Common;
using FluentValidation;

namespace DocVault.Api.Validation;

public sealed class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
  public ResetPasswordRequestValidator()
  {
    RuleFor(x => x.Email).NotEmpty().EmailAddress();
    RuleFor(x => x.Token).NotEmpty();
    RuleFor(x => x.NewPassword)
      .NotEmpty()
      .MinimumLength(ValidationConstants.Auth.MIN_PASSWORD_LENGTH)
      .MaximumLength(ValidationConstants.Auth.MAX_PASSWORD_LENGTH);
  }
}
