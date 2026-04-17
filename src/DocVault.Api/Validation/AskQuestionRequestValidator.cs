using DocVault.Api.Contracts.Qa;
using FluentValidation;

namespace DocVault.Api.Validation;

public sealed class AskQuestionRequestValidator : AbstractValidator<AskQuestionRequest>
{
  public AskQuestionRequestValidator()
  {
    RuleFor(x => x.Question)
      .NotEmpty()
      .MaximumLength(2000);

    RuleFor(x => x.MaxDocuments)
      .InclusiveBetween(1, 20);

    RuleFor(x => x.MaxContexts)
      .InclusiveBetween(1, 12);
  }
}
