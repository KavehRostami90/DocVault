using DocVault.Api.Contracts.Documents;
using FluentValidation;

namespace DocVault.Api.Validation;

public sealed class DocumentCreateRequestValidator : AbstractValidator<DocumentCreateRequest>
{
  private static readonly string[] ALLOWED_CONTENT_TYPES =
  [
    "application/pdf",
    "text/plain",
    "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
  ];

  private const long MAX_FILE_SIZE_BYTES = 50L * 1024 * 1024; // 50 MB

  public DocumentCreateRequestValidator()
  {
    RuleFor(x => x.File)
      .NotNull().WithMessage("A file must be uploaded via the 'file' form field.");

    When(x => x.File is not null, () =>
    {
      RuleFor(x => x.File!.Length)
        .GreaterThan(0).WithMessage("Uploaded file must not be empty.")
        .LessThanOrEqualTo(MAX_FILE_SIZE_BYTES).WithMessage("File must not exceed 50 MB.");

      RuleFor(x => x.File!.ContentType)
        .Must(ct => ALLOWED_CONTENT_TYPES.Contains(ct, StringComparer.OrdinalIgnoreCase))
        .WithMessage($"Content type not supported. Allowed: {string.Join(", ", ALLOWED_CONTENT_TYPES)}");
    });

    RuleFor(x => x.Title).NotEmpty().MaximumLength(256);
    RuleFor(x => x.Tags).NotNull();
  }
}
