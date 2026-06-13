using DocVault.Application.Abstractions.Persistence;
using DocVault.Application.Abstractions.Storage;
using DocVault.Application.Common.Results;
using DocVault.Application.UseCases.Documents.DeleteDocument;
using DocVault.Domain.Documents;
using DocVault.Domain.Documents.ValueObjects;
using DocVault.Domain.Storage;
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

    private static (DeleteDocumentHandler Handler, Mock<IDocumentRepository> Repo, Mock<IFileStorage> Storage, Mock<IPendingBlobDeletionRepository> Pending)
    BuildHandler(Document? toReturn = null)
    {
        var uiw     = new Mock<IUnitOfWork>();
        var repo    = new Mock<IDocumentRepository>();
        var storage = new Mock<IFileStorage>();
        var pending = new Mock<IPendingBlobDeletionRepository>();
        var handler = new DeleteDocumentHandler(repo.Object, storage.Object, uiw.Object, pending.Object);

        repo.Setup(r => r.GetAsync(It.IsAny<DocumentId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DocumentId id, CancellationToken _) =>
                toReturn is not null && id == toReturn.Id ? toReturn : null);

        return (handler, repo, storage, pending);
    }

    // -------------------------------------------------------------------------
    // Not found
    // -------------------------------------------------------------------------

    [Fact]
    public async Task HandleAsync_DocumentNotFound_ReturnsFailure()
    {
        var (handler, _, storage, _) = BuildHandler(toReturn: null);
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
        var (handler, _, storage, _) = BuildHandler(toReturn: doc);

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
        var (handler, repo, _, _) = BuildHandler(toReturn: doc);

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
        var (handler, _, storage, _) = BuildHandler(toReturn: doc);

        var command = new DeleteDocumentCommand(doc.Id, CallerId: ownerId, IsAdmin: false);

        await handler.HandleAsync(command);

        storage.Verify(s => s.DeleteAsync($"{doc.Id.Value}.bin", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_AdminCanDeleteAnyDocument()
    {
        var doc = MakeDocument(ownerId: Guid.NewGuid());
        var (handler, repo, _, _) = BuildHandler(toReturn: doc);

        var command = new DeleteDocumentCommand(doc.Id, CallerId: Guid.NewGuid(), IsAdmin: true);

        var result = await handler.HandleAsync(command);

        Assert.True(result.IsSuccess);
        repo.Verify(r => r.DeleteAsync(doc, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_AdminDelete_DeletesStorageFile()
    {
        var doc = MakeDocument(ownerId: Guid.NewGuid());
        var (handler, _, storage, _) = BuildHandler(toReturn: doc);

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
        var (handler, _, storage, _) = BuildHandler(toReturn: doc);

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
        var (handler, repo, _, _) = BuildHandler(toReturn: doc);

        var command = new DeleteDocumentCommand(doc.Id, CallerId: ownerId, IsAdmin: false);

        var result = await handler.HandleAsync(command);

        Assert.True(result.IsSuccess);
        repo.Verify(r => r.DeleteAsync(doc, It.IsAny<CancellationToken>()), Times.Once);
    }

    // -------------------------------------------------------------------------
    // Best-effort storage cleanup + pending deletion queue
    // -------------------------------------------------------------------------

    [Fact]
    public async Task HandleAsync_StorageDeleteThrows_StillReturnsSuccess()
    {
        var ownerId = Guid.NewGuid();
        var doc     = MakeDocument(ownerId: ownerId);
        var (handler, _, storage, _) = BuildHandler(toReturn: doc);

        storage.Setup(s => s.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
               .ThrowsAsync(new IOException("disk full"));

        var command = new DeleteDocumentCommand(doc.Id, CallerId: ownerId, IsAdmin: false);

        var result = await handler.HandleAsync(command);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task HandleAsync_StorageDeleteThrows_EnqueuesPendingDeletion()
    {
        var ownerId = Guid.NewGuid();
        var doc     = MakeDocument(ownerId: ownerId);
        var (handler, _, storage, pending) = BuildHandler(toReturn: doc);

        storage.Setup(s => s.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
               .ThrowsAsync(new IOException("disk full"));
        pending.Setup(p => p.ExistsByPathAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(false);

        var command = new DeleteDocumentCommand(doc.Id, CallerId: ownerId, IsAdmin: false);
        await handler.HandleAsync(command);

        pending.Verify(
            p => p.AddAsync(It.Is<PendingBlobDeletion>(e => e.StoragePath == $"{doc.Id.Value}.bin"),
                            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_StorageDeleteThrows_SkipsEnqueueWhenAlreadyQueued()
    {
        var ownerId = Guid.NewGuid();
        var doc     = MakeDocument(ownerId: ownerId);
        var (handler, _, storage, pending) = BuildHandler(toReturn: doc);

        storage.Setup(s => s.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
               .ThrowsAsync(new IOException("disk full"));
        pending.Setup(p => p.ExistsByPathAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(true);

        var command = new DeleteDocumentCommand(doc.Id, CallerId: ownerId, IsAdmin: false);
        await handler.HandleAsync(command);

        pending.Verify(p => p.AddAsync(It.IsAny<PendingBlobDeletion>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
