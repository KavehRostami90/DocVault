using DocVault.Api.Contracts.Auth.Profile;
using DocVault.Domain.Common;
using FluentValidation;

namespace DocVault.Api.Validation;

public sealed class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
{
  public UpdateProfileRequestValidator()
  {
    RuleFor(x => x.DisplayName)
      .NotEmpty()
      .MaximumLength(ValidationConstants.Auth.MAX_DISPLAY_NAME_LENGTH);
  }
}
