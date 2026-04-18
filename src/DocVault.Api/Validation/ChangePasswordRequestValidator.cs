using DocVault.Api.Contracts.Auth.Profile;
using DocVault.Domain.Common;
using FluentValidation;

namespace DocVault.Api.Validation;

public sealed class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
  public ChangePasswordRequestValidator()
  {
    RuleFor(x => x.CurrentPassword).NotEmpty();

    RuleFor(x => x.NewPassword)
      .NotEmpty()
      .MinimumLength(ValidationConstants.Auth.MIN_PASSWORD_LENGTH)
      .MaximumLength(ValidationConstants.Auth.MAX_PASSWORD_LENGTH);
  }
}
