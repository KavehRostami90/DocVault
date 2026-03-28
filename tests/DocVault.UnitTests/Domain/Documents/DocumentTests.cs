using DocVault.Domain.Documents;
using DocVault.Domain.Documents.ValueObjects;
using DocVault.Domain.Events;
using DocVault.Domain.Primitives;
using Xunit;

namespace DocVault.UnitTests.Domain.Documents;

public sealed class DocumentTests
{
    // -------------------------------------------------------------------------
    // Factory helper
    // -------------------------------------------------------------------------

    private static Document MakeDocument(string title = "Test Doc", string fileName = "test.txt",
        string contentType = "text/plain", long size = 100)
    {
        var id = DocumentId.New();
        var hash = new FileHash("abc123");
        return new Document(id, title, fileName, contentType, size, hash);
    }

    // -------------------------------------------------------------------------
    // Initial state
    // -------------------------------------------------------------------------

    [Fact]
    public void NewDocument_HasPendingStatus()
    {
        var doc = MakeDocument();
        Assert.Equal(DocumentStatus.Pending, doc.Status);
    }

    [Fact]
    public void NewDocument_HasEmptyText()
    {
        var doc = MakeDocument();
        Assert.Equal(string.Empty, doc.Text);
    }

    [Fact]
    public void NewDocument_HasNullIndexingError()
    {
        var doc = MakeDocument();
        Assert.Null(doc.IndexingError);
    }

    [Fact]
    public void NewDocument_HasNoDomainEvents()
    {
        var doc = MakeDocument();
        Assert.Empty(doc.DomainEvents);
    }

    // -------------------------------------------------------------------------
    // AttachText
    // -------------------------------------------------------------------------

    [Fact]
    public void AttachText_SetsTextProperty()
    {
        var doc = MakeDocument();
        doc.AttachText("some extracted content");
        Assert.Equal("some extracted content", doc.Text);
    }

    [Fact]
    public void AttachText_WithNull_SetsTextToEmpty()
    {
        var doc = MakeDocument();
        doc.AttachText(null!);
        Assert.Equal(string.Empty, doc.Text);
    }

    // -------------------------------------------------------------------------
    // MarkImported
    // -------------------------------------------------------------------------

    [Fact]
    public void MarkImported_SetsStatusToImported()
    {
        var doc = MakeDocument();
        doc.MarkImported();
        Assert.Equal(DocumentStatus.Imported, doc.Status);
    }

    [Fact]
    public void MarkImported_RaisesDocumentImportedEvent()
    {
        var doc = MakeDocument();
        doc.MarkImported();

        var evt = Assert.Single(doc.DomainEvents);
        var imported = Assert.IsType<DocumentImported>(evt);
        Assert.Equal(doc.Id, imported.DocumentId);
    }

    // -------------------------------------------------------------------------
    // MarkIndexed
    // -------------------------------------------------------------------------

    [Fact]
    public void MarkIndexed_SetsStatusToIndexed()
    {
        var doc = MakeDocument();
        doc.AttachText("text");
        doc.MarkIndexed();
        Assert.Equal(DocumentStatus.Indexed, doc.Status);
    }

    [Fact]
    public void MarkIndexed_RaisesDocumentIndexedEvent()
    {
        var doc = MakeDocument();
        doc.AttachText("text");
        doc.MarkIndexed();

        var evt = Assert.Single(doc.DomainEvents);
        var indexed = Assert.IsType<DocumentIndexed>(evt);
        Assert.Equal(doc.Id, indexed.DocumentId);
    }

    [Fact]
    public void MarkIndexed_DoesNotClearPreviousEvents()
    {
        var doc = MakeDocument();
        doc.MarkImported();   // raises DocumentImported
        doc.MarkIndexed();    // raises DocumentIndexed
        Assert.Equal(2, doc.DomainEvents.Count);
    }

    // -------------------------------------------------------------------------
    // MarkFailed
    // -------------------------------------------------------------------------

    [Fact]
    public void MarkFailed_WithoutMessage_SetsStatusToFailed()
    {
        var doc = MakeDocument();
        doc.MarkFailed();
        Assert.Equal(DocumentStatus.Failed, doc.Status);
    }

    [Fact]
    public void MarkFailed_WithoutMessage_LeavesIndexingErrorNull()
    {
        var doc = MakeDocument();
        doc.MarkFailed();
        Assert.Null(doc.IndexingError);
    }

    [Fact]
    public void MarkFailed_WithMessage_StoresIndexingError()
    {
        var doc = MakeDocument();
        doc.MarkFailed("File could not be read");
        Assert.Equal(DocumentStatus.Failed, doc.Status);
        Assert.Equal("File could not be read", doc.IndexingError);
    }

    [Fact]
    public void MarkFailed_DoesNotRaiseAnyDomainEvent()
    {
        var doc = MakeDocument();
        doc.MarkFailed("error");
        Assert.Empty(doc.DomainEvents);
    }

    // -------------------------------------------------------------------------
    // Domain events — ClearDomainEvents
    // -------------------------------------------------------------------------

    [Fact]
    public void ClearDomainEvents_RemovesAllRaisedEvents()
    {
        var doc = MakeDocument();
        doc.MarkImported();
        doc.MarkIndexed();
        Assert.Equal(2, doc.DomainEvents.Count);

        doc.ClearDomainEvents();
        Assert.Empty(doc.DomainEvents);
    }

    // -------------------------------------------------------------------------
    // Constructor validation
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Constructor_EmptyTitle_ThrowsDomainException(string title)
    {
        var id = DocumentId.New();
        var hash = new FileHash("h");
        Assert.Throws<DomainException>(() =>
            new Document(id, title, "file.txt", "text/plain", 100, hash));
    }

    [Theory]
    [InlineData("..")]
    [InlineData("path/../other")]
    [InlineData("path/file")]
    [InlineData("path\\file")]
    public void Constructor_InvalidFileName_ThrowsDomainException(string fileName)
    {
        var id = DocumentId.New();
        var hash = new FileHash("h");
        Assert.Throws<DomainException>(() =>
            new Document(id, "Title", fileName, "text/plain", 100, hash));
    }
}

