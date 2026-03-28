using DocVault.Domain.Documents;
using DocVault.Domain.Documents.ValueObjects;

namespace DocVault.IntegrationTests.Infrastructure;

/// <summary>
/// Builder pattern for creating test documents.
/// Follows the Builder pattern and Single Responsibility Principle.
/// </summary>
public sealed class DocumentTestBuilder
{
    private string _title = "Test Document";
    private string _content = "Test content";
    private string _fileName = DocumentTestConstants.DEFAULT_FILENAME;
    private string _contentType = DocumentTestConstants.DEFAULT_CONTENT_TYPE;

    public DocumentTestBuilder WithTitle(string title)
    {
        _title = title;
        return this;
    }

    public DocumentTestBuilder WithContent(string content)
    {
        _content = content;
        return this;
    }

    public DocumentTestBuilder WithFileName(string fileName)
    {
        _fileName = fileName;
        return this;
    }

    public DocumentTestBuilder WithContentType(string contentType)
    {
        _contentType = contentType;
        return this;
    }

    public Document Build()
    {
        var id = DocumentId.New();
        var hash = new FileHash(Guid.NewGuid().ToString("N"));
        var document = new Document(id, _title, _fileName, _contentType, _content.Length, hash);
        document.AttachText(_content);
        document.MarkIndexed();
        return document;
    }

    public static DocumentTestBuilder New() => new();
}

