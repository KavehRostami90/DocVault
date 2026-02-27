using DocVault.Domain.Documents.ValueObjects;
using DocVault.Domain.Primitives;

namespace DocVault.Domain.Documents;

public class Document : AggregateRoot<DocumentId>
{
  private readonly List<Tag> _tags = new();

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
    Title = title;
    FileName = fileName;
    ContentType = contentType;
    Size = size;
    Hash = hash;
    Text = string.Empty;
    Status = DocumentStatus.Pending;
  }

  public void AttachText(string text)
  {
    Text = text;
    Touch();
  }

  public void MarkIndexed() => Status = DocumentStatus.Indexed;
  public void MarkImported() => Status = DocumentStatus.Imported;
  public void MarkFailed() => Status = DocumentStatus.Failed;

  public void ReplaceTags(IEnumerable<Tag> tags)
  {
    _tags.Clear();
    _tags.AddRange(tags);
    Touch();
  }
}
