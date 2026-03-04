using DocVault.Api.Contracts.Documents;
using FluentValidation;

namespace DocVault.Api.Validation;

public sealed class DocumentUpdateRequestValidator : AbstractValidator<DocumentUpdateRequest>
{
  private const int MAX_TAG_LENGTH = 50;
  private const int MAX_TAGS_COUNT = 20;

  public DocumentUpdateRequestValidator()
  {
    RuleFor(x => x.Tags)
      .NotNull()
      .WithMessage("Tags collection cannot be null.")
      .Must(tags => tags.Count <= MAX_TAGS_COUNT)
      .WithMessage($"Cannot have more than {MAX_TAGS_COUNT} tags.");

    RuleForEach(x => x.Tags)
      .NotEmpty()
      .WithMessage("Tag cannot be empty.")
      .MaximumLength(MAX_TAG_LENGTH)
      .WithMessage($"Tag cannot exceed {MAX_TAG_LENGTH} characters.")
      .Must(tag => !string.IsNullOrWhiteSpace(tag?.Trim()))
      .WithMessage("Tag cannot be only whitespace.")
      .Must(tag => tag.All(c => char.IsLetterOrDigit(c) || c == '-' || c == '_'))
      .WithMessage("Tag can only contain letters, digits, hyphens, and underscores.");

    // Ensure no duplicate tags
    RuleFor(x => x.Tags)
      .Must(tags => tags.Distinct(StringComparer.OrdinalIgnoreCase).Count() == tags.Count)
      .WithMessage("Tags cannot contain duplicates.");
  }
}
