using DocVault.Domain.Common;
using DocVault.Domain.Documents.ValueObjects;
using DocVault.Domain.Events;
using DocVault.Domain.Primitives;

namespace DocVault.Domain.Documents;

public class Document : AggregateRoot<DocumentId>
{
  private readonly List<Tag> _tags = new();

  private static readonly string[] s_invalidFileNamePatterns = ["..", "/", "\\"];

  public string Title { get; private set; }
  public string FileName { get; private set; }
  public string ContentType { get; private set; }
  public long Size { get; private set; }
  public FileHash Hash { get; private set; }
  public string Text { get; private set; }
  public float[]? Embedding { get; private set; }
  public DocumentStatus Status { get; private set; }
  /// <summary>Populated when indexing fails. Null otherwise.</summary>
  public string? IndexingError { get; private set; }
  /// <summary>Owner's identity user id. Null for documents imported before auth was added.</summary>
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

  public void AttachEmbedding(float[] embedding)
  {
    Embedding = embedding;
    Touch();
  }

  public void MarkIndexed()
  {
    if (Status != DocumentStatus.Imported)
      throw new DomainException(DomainErrorCodes.InvalidStateTransition,
        $"Cannot mark document as Indexed from {Status} state — must be Imported first.");

    Status = DocumentStatus.Indexed;
    Touch();
    RaiseDomainEvent(new DocumentIndexed(Id));
  }

  public void MarkImported()
  {
    if (Status != DocumentStatus.Pending)
      throw new DomainException(DomainErrorCodes.InvalidStateTransition,
        $"Cannot mark document as Imported from {Status} state — must be Pending.");

    Status = DocumentStatus.Imported;
    Touch();
    RaiseDomainEvent(new DocumentImported(Id));
  }

  /// <summary>
  /// Resets the document to the <see cref="DocumentStatus.Imported"/> state so it can be
  /// re-processed by the indexing pipeline. Valid from <c>Indexed</c>, <c>Failed</c>, or
  /// <c>Imported</c> (document stuck from a previous incomplete run).
  /// </summary>
  public void PrepareForReindex()
  {
    if (Status is DocumentStatus.Pending)
      throw new DomainException(DomainErrorCodes.InvalidStateTransition,
        $"Cannot re-queue document from {Status} state — it has not been imported yet.");

    Status = DocumentStatus.Imported;
    Touch();
  }

  public void MarkFailed(string? error = null)
  {
    if (Status is DocumentStatus.Indexed or DocumentStatus.Failed)
      throw new DomainException(DomainErrorCodes.InvalidStateTransition,
        $"Cannot mark a {Status} document as Failed.");

    Status = DocumentStatus.Failed;
    IndexingError = error;
    Touch();
    RaiseDomainEvent(new DocumentFailed(Id, error));
  }

  public void ReplaceTags(IEnumerable<Tag> tags)
  {
    var tagsList = tags.ToList();

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

    if (s_invalidFileNamePatterns.Any(fileName.Contains))
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
