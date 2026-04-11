using DocVault.Api.Contracts.Documents;
using DocVault.Api.Composition;
using DocVault.Domain.Common;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace DocVault.Api.Validation;

public sealed class DocumentCreateRequestValidator : AbstractValidator<DocumentCreateRequest>
{
  public DocumentCreateRequestValidator(IOptions<UploadOptions> uploadOptions)
  {
    var maxFileSizeBytes = uploadOptions.Value.MaxFileSizeBytes;

    RuleFor(x => x.File)
      .NotNull()
      .WithMessage("A file must be uploaded via the 'file' form field.");

    When(x => x.File is not null, () =>
    {
      RuleFor(x => x.File!.Length)
        .InclusiveBetween(ValidationConstants.Documents.MIN_FILE_SIZE_BYTES, maxFileSizeBytes)
        .WithMessage($"File size must be between {ValidationConstants.Documents.MIN_FILE_SIZE_BYTES} bytes and {FormatFileSize(maxFileSizeBytes)}.");

      RuleFor(x => x.File!.ContentType)
        .NotEmpty()
        .WithMessage("File content type is required.")
        .Must(ct => ValidationConstants.Documents.ALLOWED_CONTENT_TYPES.Contains(ct, StringComparer.OrdinalIgnoreCase))
        .WithMessage($"Content type not supported. Allowed: {string.Join(", ", ValidationConstants.Documents.ALLOWED_CONTENT_TYPES)}");

      RuleFor(x => x.File!.FileName)
        .NotEmpty()
        .WithMessage("File name is required.")
        .Must(fileName => !string.IsNullOrWhiteSpace(fileName))
        .WithMessage("File name cannot be only whitespace.")
        .Must(fileName => !fileName.Contains("..") && !fileName.Contains("/") && !fileName.Contains("\\"))
        .WithMessage("File name contains invalid characters.");
    });

    RuleFor(x => x.Title)
      .NotEmpty()
      .WithMessage("Title is required.")
      .Length(ValidationConstants.Documents.MIN_TITLE_LENGTH, ValidationConstants.Documents.MAX_TITLE_LENGTH)
      .WithMessage($"Title must be between {ValidationConstants.Documents.MIN_TITLE_LENGTH} and {ValidationConstants.Documents.MAX_TITLE_LENGTH} characters.")
      .Must(title => !string.IsNullOrWhiteSpace(title?.Trim()))
      .WithMessage("Title cannot be only whitespace.");

    RuleFor(x => x.Tags)
      .NotNull()
      .WithMessage("Tags collection cannot be null.")
      .Must(tags => tags.Count <= ValidationConstants.Tags.MAX_TAGS_PER_DOCUMENT)
      .WithMessage($"Cannot have more than {ValidationConstants.Tags.MAX_TAGS_PER_DOCUMENT} tags.");

    RuleForEach(x => x.Tags)
      .NotEmpty()
      .WithMessage("Tag cannot be empty.")
      .MaximumLength(ValidationConstants.Tags.MAX_NAME_LENGTH)
      .WithMessage($"Tag cannot exceed {ValidationConstants.Tags.MAX_NAME_LENGTH} characters.")
      .Must(tag => !string.IsNullOrWhiteSpace(tag?.Trim()))
      .WithMessage("Tag cannot be only whitespace.")
      .Must(tag => tag.All(c => char.IsLetterOrDigit(c) || c == '-' || c == '_'))
      .WithMessage("Tag can only contain letters, digits, hyphens, and underscores.");

    // Ensure no duplicate tags
    RuleFor(x => x.Tags)
      .Must(tags => tags.Distinct(StringComparer.OrdinalIgnoreCase).Count() == tags.Count)
      .WithMessage("Tags cannot contain duplicates.");
  }

  private static string FormatFileSize(long bytes)
  {
    if (bytes < 1024) return $"{bytes} bytes";
    if (bytes < 1024 * 1024) return $"{bytes / 1024d:F1} KB";
    return $"{bytes / 1024d / 1024d:F1} MB";
  }
}
