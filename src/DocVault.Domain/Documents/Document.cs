using DocVault.Domain.Common;
using DocVault.Domain.Documents.ValueObjects;
using DocVault.Domain.Events;
using DocVault.Domain.Primitives;

namespace DocVault.Domain.Documents;

public class Document : AggregateRoot<DocumentId>
{
  private readonly List<Tag> _tags = new();

  // Invalid patterns that must not appear in a stored file name.
  private static readonly string[] INVALID_FILE_NAME_PATTERNS = ["..", "/", "\\"];

  public string Title { get; private set; }
  public string FileName { get; private set; }
  public string ContentType { get; private set; }
  public long Size { get; private set; }
  public FileHash Hash { get; private set; }
  public string Text { get; private set; }
  public DocumentStatus Status { get; private set; }
  /// <summary>Set when the document enters the <see cref="DocumentStatus.Failed"/> state.</summary>
  public string? IndexingError { get; private set; }
  /// <summary>Identity user id of the owner. Null for documents created before auth was introduced.</summary>
  public Guid? OwnerId { get; private set; }
  public IReadOnlyCollection<Tag> Tags => _tags.AsReadOnly();

  private Document() : base(default)
  {
    Title = string.Empty;
    FileName = string.Empty;
    ContentType = string.Empty;
    Text = string.Empty;
    Hash = new FileHash(string.Empty);
  }

  public Document(DocumentId id, string title, string fileName, string contentType, long size, FileHash hash, Guid? ownerId = null) : base(id)
  {
    SetTitle(title);
    SetFileName(fileName);
    SetContentType(contentType);
    SetSize(size);
    Hash = hash;
    Text = string.Empty;
    Status = DocumentStatus.Pending;
    OwnerId = ownerId;
  }

  public void AttachText(string text)
  {
    Text = text ?? string.Empty;
    Touch();
  }

  public void MarkIndexed()
  {
    Status = DocumentStatus.Indexed;
    RaiseDomainEvent(new DocumentIndexed(Id));
  }

  public void MarkImported()
  {
    Status = DocumentStatus.Imported;
    RaiseDomainEvent(new DocumentImported(Id));
  }

  public void MarkFailed(string? error = null)
  {
    Status = DocumentStatus.Failed;
    IndexingError = error;
  }

  public void ReplaceTags(IEnumerable<Tag> tags)
  {
    var tagsList = tags?.ToList() ?? throw new DomainException(DomainErrorCodes.TagsRequired, "Tags cannot be null");

    if (tagsList.Count > ValidationConstants.Tags.MAX_TAGS_PER_DOCUMENT)
      throw new DomainException(DomainErrorCodes.TagLimitExceeded, $"Cannot have more than {ValidationConstants.Tags.MAX_TAGS_PER_DOCUMENT} tags");

    _tags.Clear();
    _tags.AddRange(tagsList);
    Touch();
  }

  private void SetTitle(string title)
  {
    if (string.IsNullOrWhiteSpace(title))
      throw new DomainException(DomainErrorCodes.TitleRequired, "Title cannot be empty or whitespace");

    if (title.Length < ValidationConstants.Documents.MIN_TITLE_LENGTH || title.Length > ValidationConstants.Documents.MAX_TITLE_LENGTH)
      throw new DomainException(DomainErrorCodes.TitleLength, $"Title must be between {ValidationConstants.Documents.MIN_TITLE_LENGTH} and {ValidationConstants.Documents.MAX_TITLE_LENGTH} characters");

    Title = title.Trim();
  }

  private void SetFileName(string fileName)
  {
    if (string.IsNullOrWhiteSpace(fileName))
      throw new DomainException(DomainErrorCodes.FileNameRequired, "File name cannot be empty or whitespace");

    if (INVALID_FILE_NAME_PATTERNS.Any(fileName.Contains))
      throw new DomainException(DomainErrorCodes.FileNameInvalid, "File name contains invalid characters");

    FileName = fileName.Trim();
  }

  private void SetContentType(string contentType)
  {
    if (string.IsNullOrWhiteSpace(contentType))
      throw new DomainException(DomainErrorCodes.ContentTypeRequired, "Content type cannot be empty");

    ContentType = contentType.Trim();
  }

  private void SetSize(long size)
  {
    if (size < ValidationConstants.Documents.MIN_FILE_SIZE_BYTES || size > ValidationConstants.Documents.MAX_FILE_SIZE_BYTES)
      throw new DomainException(DomainErrorCodes.FileSizeOutOfRange, $"File size must be between {ValidationConstants.Documents.MIN_FILE_SIZE_BYTES} bytes and {ValidationConstants.Documents.MAX_FILE_SIZE_BYTES / (1024 * 1024)} MB");

    Size = size;
  }
}
