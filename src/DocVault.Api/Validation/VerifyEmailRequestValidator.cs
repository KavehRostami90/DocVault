using DocVault.Api.Contracts.Auth;
using DocVault.Domain.Common;
using FluentValidation;

namespace DocVault.Api.Validation;

public sealed class VerifyEmailRequestValidator : AbstractValidator<VerifyEmailRequest>
{
  public VerifyEmailRequestValidator()
  {
    RuleFor(x => x.Email)
      .NotEmpty()
      .EmailAddress()
      .MaximumLength(ValidationConstants.Auth.MAX_EMAIL_LENGTH);
    RuleFor(x => x.Token).NotEmpty();
  }
}
