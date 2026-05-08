using DocVault.Api.Contracts.Auth;
using DocVault.Domain.Common;
using FluentValidation;

namespace DocVault.Api.Validation;

public sealed class ResendVerificationRequestValidator : AbstractValidator<ResendVerificationRequest>
{
  public ResendVerificationRequestValidator()
  {
    RuleFor(x => x.Email)
      .NotEmpty()
      .EmailAddress()
      .MaximumLength(ValidationConstants.Auth.MAX_EMAIL_LENGTH);
  }
}
