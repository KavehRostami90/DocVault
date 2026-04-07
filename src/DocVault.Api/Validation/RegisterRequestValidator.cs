using DocVault.Api.Contracts.Auth;
using DocVault.Domain.Common;
using FluentValidation;

namespace DocVault.Api.Validation;

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
  public RegisterRequestValidator()
  {
    RuleFor(x => x.Email)
      .NotEmpty()
      .EmailAddress()
      .MaximumLength(ValidationConstants.Auth.MAX_EMAIL_LENGTH);

    RuleFor(x => x.Password)
      .NotEmpty()
      .MinimumLength(ValidationConstants.Auth.MIN_PASSWORD_LENGTH)
      .MaximumLength(ValidationConstants.Auth.MAX_PASSWORD_LENGTH);

    RuleFor(x => x.DisplayName)
      .NotEmpty()
      .MaximumLength(ValidationConstants.Auth.MAX_DISPLAY_NAME_LENGTH);
  }
}
