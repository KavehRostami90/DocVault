using DocVault.Api.Contracts.Auth;
using FluentValidation;

namespace DocVault.Api.Validation;

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
  public LoginRequestValidator()
  {
    RuleFor(x => x.Email).NotEmpty().EmailAddress();
    RuleFor(x => x.Password).NotEmpty();
  }
}
