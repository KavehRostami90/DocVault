using DocVault.Domain.Documents.ValueObjects;
using DocVault.Domain.Primitives;

namespace DocVault.Domain.Documents;

public class Document : AggregateRoot<DocumentId>
{
  private readonly List<Tag> _tags = new();

  private const int MAX_TITLE_LENGTH = 256;
  private const int MIN_TITLE_LENGTH = 1;
  private const long MAX_FILE_SIZE = 50L * 1024 * 1024; // 50 MB
  private const long MIN_FILE_SIZE = 1;

  public string Title { get; private set; }
  public string FileName { get; private set; }
  public string ContentType { get; private set; }
  public long Size { get; private set; }
  public FileHash Hash { get; private set; }
  public string Text { get; private set; }
  public DocumentStatus Status { get; private set; }
  public IReadOnlyCollection<Tag> Tags => _tags.AsReadOnly();

  private Document() : base(DocumentId.New())
  {
    Title = string.Empty;
    FileName = string.Empty;
    ContentType = string.Empty;
    Text = string.Empty;
    Hash = new FileHash(string.Empty);
  }

  public Document(DocumentId id, string title, string fileName, string contentType, long size, FileHash hash) : base(id)
  {
    SetTitle(title);
    SetFileName(fileName);
    SetContentType(contentType);
    SetSize(size);
    Hash = hash;
    Text = string.Empty;
    Status = DocumentStatus.Pending;
  }

  public void AttachText(string text)
  {
    Text = text ?? string.Empty;
    Touch();
  }

  public void MarkIndexed() => Status = DocumentStatus.Indexed;
  public void MarkImported() => Status = DocumentStatus.Imported;
  public void MarkFailed() => Status = DocumentStatus.Failed;

  public void ReplaceTags(IEnumerable<Tag> tags)
  {
    var tagsList = tags?.ToList() ?? throw new DomainException("Tags cannot be null");

    if (tagsList.Count > 20)
      throw new DomainException("Cannot have more than 20 tags");

    _tags.Clear();
    _tags.AddRange(tagsList);
    Touch();
  }

  private void SetTitle(string title)
  {
    if (string.IsNullOrWhiteSpace(title))
      throw new DomainException("Title cannot be empty or whitespace");

    if (title.Length < MIN_TITLE_LENGTH || title.Length > MAX_TITLE_LENGTH)
      throw new DomainException($"Title must be between {MIN_TITLE_LENGTH} and {MAX_TITLE_LENGTH} characters");

    Title = title.Trim();
  }

  private void SetFileName(string fileName)
  {
    if (string.IsNullOrWhiteSpace(fileName))
      throw new DomainException("File name cannot be empty or whitespace");

    if (fileName.Contains("..") || fileName.Contains("/") || fileName.Contains("\\"))
      throw new DomainException("File name contains invalid characters");

    FileName = fileName.Trim();
  }

  private void SetContentType(string contentType)
  {
    if (string.IsNullOrWhiteSpace(contentType))
      throw new DomainException("Content type cannot be empty");

    ContentType = contentType.Trim();
  }

  private void SetSize(long size)
  {
    if (size < MIN_FILE_SIZE || size > MAX_FILE_SIZE)
      throw new DomainException($"File size must be between {MIN_FILE_SIZE} bytes and {MAX_FILE_SIZE / (1024 * 1024)} MB");

    Size = size;
  }
}
