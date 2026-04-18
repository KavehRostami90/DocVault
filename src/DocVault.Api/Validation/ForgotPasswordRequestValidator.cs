using DocVault.Api.Contracts.Auth;
using DocVault.Domain.Common;
using FluentValidation;

namespace DocVault.Api.Validation;

public sealed class ForgotPasswordRequestValidator : AbstractValidator<ForgotPasswordRequest>
{
  public ForgotPasswordRequestValidator()
  {
    RuleFor(x => x.Email)
      .NotEmpty()
      .EmailAddress()
      .MaximumLength(ValidationConstants.Auth.MAX_EMAIL_LENGTH);
  }
}
