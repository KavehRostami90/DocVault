using DocVault.Application.Abstractions.Persistence;
using DocVault.Application.Abstractions.Storage;
using DocVault.Application.Common.Results;
using DocVault.Application.UseCases.Documents.DeleteDocument;
using DocVault.Domain.Documents;
using DocVault.Domain.Documents.ValueObjects;
using Moq;
using Xunit;

namespace DocVault.UnitTests.Application;

public sealed class DeleteDocumentHandlerTests
{
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static Document MakeDocument(DocumentStatus status = DocumentStatus.Indexed, Guid? ownerId = null)
    {
        var id   = DocumentId.New();
        var hash = new FileHash("abc123");
        var doc  = new Document(id, "Title", "file.txt", "text/plain", 100, hash, ownerId);
        if (status == DocumentStatus.Imported) doc.MarkImported();
        else if (status == DocumentStatus.Indexed) { doc.MarkImported(); doc.AttachText("text"); doc.MarkIndexed(); }
        else if (status == DocumentStatus.Failed)  { doc.MarkImported(); doc.MarkFailed("err"); }
        return doc;
    }

       private static (DeleteDocumentHandler Handler, Mock<IDocumentRepository> Repo, Mock<IFileStorage> Storage)
    BuildHandler(Document? toReturn = null)
    {
    var uiw = new Mock<IUnitOfWork>();
        var repo    = new Mock<IDocumentRepository>();
        var storage = new Mock<IFileStorage>();
        var handler = new DeleteDocumentHandler(repo.Object, storage.Object, uiw.Object);

        repo.Setup(r => r.GetAsync(It.IsAny<DocumentId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DocumentId id, CancellationToken _) =>
                toReturn is not null && id == toReturn.Id ? toReturn : null);

        return (handler, repo, storage);
    }

    // -------------------------------------------------------------------------
    // Not found
    // -------------------------------------------------------------------------

    [Fact]
    public async Task HandleAsync_DocumentNotFound_ReturnsFailure()
    {
        var (handler, _, storage) = BuildHandler(toReturn: null);
        var command = new DeleteDocumentCommand(DocumentId.New(), Guid.NewGuid());

        var result = await handler.HandleAsync(command);

        Assert.False(result.IsSuccess);
        Assert.Equal(Errors.NotFound, result.Error);
        storage.Verify(s => s.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // -------------------------------------------------------------------------
    // Ownership checks
    // -------------------------------------------------------------------------

    [Fact]
    public async Task HandleAsync_NonAdminDifferentOwner_ReturnsNotFound()
    {
        var ownerId = Guid.NewGuid();
        var doc     = MakeDocument(ownerId: ownerId);
        var (handler, _, storage) = BuildHandler(toReturn: doc);

        var command = new DeleteDocumentCommand(doc.Id, CallerId: Guid.NewGuid(), IsAdmin: false);

        var result = await handler.HandleAsync(command);

        Assert.False(result.IsSuccess);
        Assert.Equal(Errors.NotFound, result.Error);
        storage.Verify(s => s.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_NonAdminSameOwner_DeletesDocument()
    {
        var ownerId = Guid.NewGuid();
        var doc     = MakeDocument(ownerId: ownerId);
        var (handler, repo, _) = BuildHandler(toReturn: doc);

        var command = new DeleteDocumentCommand(doc.Id, CallerId: ownerId, IsAdmin: false);

        var result = await handler.HandleAsync(command);

        Assert.True(result.IsSuccess);
        repo.Verify(r => r.DeleteAsync(doc, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_NonAdminSameOwner_DeletesStorageFile()
    {
        var ownerId = Guid.NewGuid();
        var doc     = MakeDocument(ownerId: ownerId);
        var (handler, _, storage) = BuildHandler(toReturn: doc);

        var command = new DeleteDocumentCommand(doc.Id, CallerId: ownerId, IsAdmin: false);

        await handler.HandleAsync(command);

        storage.Verify(s => s.DeleteAsync($"{doc.Id.Value}.bin", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_AdminCanDeleteAnyDocument()
    {
        var doc = MakeDocument(ownerId: Guid.NewGuid());
        var (handler, repo, _) = BuildHandler(toReturn: doc);

        var command = new DeleteDocumentCommand(doc.Id, CallerId: Guid.NewGuid(), IsAdmin: true);

        var result = await handler.HandleAsync(command);

        Assert.True(result.IsSuccess);
        repo.Verify(r => r.DeleteAsync(doc, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_AdminDelete_DeletesStorageFile()
    {
        var doc = MakeDocument(ownerId: Guid.NewGuid());
        var (handler, _, storage) = BuildHandler(toReturn: doc);

        var command = new DeleteDocumentCommand(doc.Id, CallerId: Guid.NewGuid(), IsAdmin: true);

        await handler.HandleAsync(command);

        storage.Verify(s => s.DeleteAsync($"{doc.Id.Value}.bin", It.IsAny<CancellationToken>()), Times.Once);
    }

    // -------------------------------------------------------------------------
    // Conflict: pending document
    // -------------------------------------------------------------------------

    [Fact]
    public async Task HandleAsync_PendingDocument_ReturnsConflict()
    {
        var ownerId = Guid.NewGuid();
        var doc = new Document(DocumentId.New(), "T", "f.txt", "text/plain", 100, new FileHash("h"), ownerId);
        var (handler, _, storage) = BuildHandler(toReturn: doc);

        var command = new DeleteDocumentCommand(doc.Id, CallerId: ownerId, IsAdmin: false);

        var result = await handler.HandleAsync(command);

        Assert.False(result.IsSuccess);
        Assert.Equal(Errors.Conflict, result.Error);
        storage.Verify(s => s.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // -------------------------------------------------------------------------
    // Success for various terminal statuses
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(DocumentStatus.Indexed)]
    [InlineData(DocumentStatus.Failed)]
    [InlineData(DocumentStatus.Imported)]
    public async Task HandleAsync_TerminalOrImportedStatus_Succeeds(DocumentStatus status)
    {
        var ownerId = Guid.NewGuid();
        var doc     = MakeDocument(status, ownerId);
        var (handler, repo, _) = BuildHandler(toReturn: doc);

        var command = new DeleteDocumentCommand(doc.Id, CallerId: ownerId, IsAdmin: false);

        var result = await handler.HandleAsync(command);

        Assert.True(result.IsSuccess);
        repo.Verify(r => r.DeleteAsync(doc, It.IsAny<CancellationToken>()), Times.Once);
    }

    // -------------------------------------------------------------------------
    // Best-effort storage cleanup
    // -------------------------------------------------------------------------

    [Fact]
    public async Task HandleAsync_StorageDeleteThrows_StillReturnsSuccess()
    {
        var ownerId = Guid.NewGuid();
        var doc     = MakeDocument(ownerId: ownerId);
        var (handler, _, storage) = BuildHandler(toReturn: doc);

        storage.Setup(s => s.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
               .ThrowsAsync(new IOException("disk full"));

        var command = new DeleteDocumentCommand(doc.Id, CallerId: ownerId, IsAdmin: false);

        var result = await handler.HandleAsync(command);

        Assert.True(result.IsSuccess);
    }
}
