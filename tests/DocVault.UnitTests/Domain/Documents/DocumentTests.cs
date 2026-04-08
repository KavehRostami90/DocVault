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
    public void MarkImported_WhenNotPending_ThrowsDomainException()
    {
        var doc = MakeDocument();
        doc.MarkImported(); // Pending → Imported
        Assert.Throws<DomainException>(() => doc.MarkImported()); // Imported → error
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

    [Fact]
    public void MarkImported_SetsUpdatedAt()
    {
        var doc = MakeDocument();
        Assert.Null(doc.UpdatedAt);
        doc.MarkImported();
        Assert.NotNull(doc.UpdatedAt);
    }

    // -------------------------------------------------------------------------
    // MarkIndexed
    // -------------------------------------------------------------------------

    [Fact]
    public void MarkIndexed_SetsStatusToIndexed()
    {
        var doc = MakeDocument();
        doc.MarkImported();
        doc.AttachText("text");
        doc.MarkIndexed();
        Assert.Equal(DocumentStatus.Indexed, doc.Status);
    }

    [Fact]
    public void MarkIndexed_RaisesDocumentIndexedEvent()
    {
        var doc = MakeDocument();
        doc.MarkImported();
        doc.ClearDomainEvents(); // ignore the Imported event
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

    [Fact]
    public void MarkIndexed_WhenNotImported_ThrowsDomainException()
    {
        var doc = MakeDocument(); // Pending state
        Assert.Throws<DomainException>(() => doc.MarkIndexed());
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
    public void MarkFailed_WhenAlreadyIndexed_ThrowsDomainException()
    {
        var doc = MakeDocument();
        doc.MarkImported();
        doc.MarkIndexed();
        Assert.Throws<DomainException>(() => doc.MarkFailed("too late"));
    }

    [Fact]
    public void MarkFailed_WhenAlreadyFailed_ThrowsDomainException()
    {
        var doc = MakeDocument();
        doc.MarkFailed("first failure");
        Assert.Throws<DomainException>(() => doc.MarkFailed("second failure"));
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
    public void MarkFailed_RaisesDocumentFailedEvent()
    {
        var doc = MakeDocument();
        doc.MarkFailed("error");

        var evt = Assert.Single(doc.DomainEvents);
        var failed = Assert.IsType<DocumentFailed>(evt);
        Assert.Equal(doc.Id, failed.DocumentId);
        Assert.Equal("error", failed.Error);
    }

    [Fact]
    public void MarkFailed_SetsUpdatedAt()
    {
        var doc = MakeDocument();
        Assert.Null(doc.UpdatedAt);
        doc.MarkFailed();
        Assert.NotNull(doc.UpdatedAt);
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

