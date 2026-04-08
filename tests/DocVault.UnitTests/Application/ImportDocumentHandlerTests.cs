using DocVault.Application.Abstractions.Persistence;
using DocVault.Application.Abstractions.Storage;
using DocVault.Application.Background.Queue;
using DocVault.Application.Common.Results;
using DocVault.Application.UseCases.Documents.ImportDocument;
using DocVault.Domain.Documents;
using DocVault.Domain.Documents.ValueObjects;
using DocVault.Domain.Imports;
using Moq;
using Xunit;

namespace DocVault.UnitTests.Application;

public sealed class ImportDocumentHandlerTests
{
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static ImportDocumentCommand MakeCommand(string title = "My Doc", string fileName = "doc.txt",
        string contentType = "text/plain", long size = 100, Guid? ownerId = null)
    {
        var content = new MemoryStream(new byte[size > 0 ? (int)size : 1]);
        return new ImportDocumentCommand(title, fileName, contentType, size, [], content, ownerId);
    }

    private static (
        ImportDocumentHandler Handler,
        Mock<IDocumentRepository> DocRepo,
        Mock<IImportJobRepository> JobRepo,
        Mock<IFileStorage> Storage,
        Mock<IWorkQueue<IndexingWorkItem>> Queue,
        Mock<IUnitOfWork> UoW)
    BuildHandler()
    {
        var docRepo = new Mock<IDocumentRepository>();
        var jobRepo = new Mock<IImportJobRepository>();
        var storage = new Mock<IFileStorage>();
        var queue   = new Mock<IWorkQueue<IndexingWorkItem>>();
        var uow     = new Mock<IUnitOfWork>();

        // UoW executes action inline (no real transaction in unit tests)
        uow.Setup(u => u.ExecuteInTransactionAsync(It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()))
           .Returns<Func<CancellationToken, Task>, CancellationToken>((action, ct) => action(ct));

        var handler = new ImportDocumentHandler(
            docRepo.Object, jobRepo.Object, storage.Object, queue.Object, uow.Object);
        return (handler, docRepo, jobRepo, storage, queue, uow);
    }

    // -------------------------------------------------------------------------
    // Success path
    // -------------------------------------------------------------------------

    [Fact]
    public async Task HandleAsync_ValidCommand_ReturnsSuccessResult()
    {
        var (handler, _, _, _, _, _) = BuildHandler();

        var result = await handler.HandleAsync(MakeCommand());

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
    }

    [Fact]
    public async Task HandleAsync_ValidCommand_AddsDocumentToRepository()
    {
        var (handler, docRepo, _, _, _, _) = BuildHandler();

        await handler.HandleAsync(MakeCommand());

        docRepo.Verify(r => r.AddAsync(It.IsAny<Document>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ValidCommand_AddsImportJobToRepository()
    {
        var (handler, _, jobRepo, _, _, _) = BuildHandler();

        await handler.HandleAsync(MakeCommand());

        jobRepo.Verify(r => r.AddAsync(It.IsAny<ImportJob>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ValidCommand_EnqueuesWorkItem()
    {
        var (handler, _, _, _, queue, _) = BuildHandler();

        await handler.HandleAsync(MakeCommand());

        queue.Verify(q => q.Enqueue(It.IsAny<IndexingWorkItem>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ValidCommand_WritesFileToStorage()
    {
        var (handler, _, _, storage, _, _) = BuildHandler();

        await handler.HandleAsync(MakeCommand());

        storage.Verify(s => s.WriteAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ValidCommand_DocumentHasImportedStatus()
    {
        Document? captured = null;
        var (handler, docRepo, _, _, _, _) = BuildHandler();
        docRepo.Setup(r => r.AddAsync(It.IsAny<Document>(), It.IsAny<CancellationToken>()))
               .Callback<Document, CancellationToken>((doc, _) => captured = doc);

        await handler.HandleAsync(MakeCommand());

        Assert.NotNull(captured);
        Assert.Equal(DocumentStatus.Imported, captured!.Status);
    }

    [Fact]
    public async Task HandleAsync_ValidCommand_DocumentOwnerIdMatches()
    {
        var ownerId = Guid.NewGuid();
        Document? captured = null;
        var (handler, docRepo, _, _, _, _) = BuildHandler();
        docRepo.Setup(r => r.AddAsync(It.IsAny<Document>(), It.IsAny<CancellationToken>()))
               .Callback<Document, CancellationToken>((doc, _) => captured = doc);

        await handler.HandleAsync(MakeCommand(ownerId: ownerId));

        Assert.Equal(ownerId, captured!.OwnerId);
    }

    // -------------------------------------------------------------------------
    // Transaction atomicity
    // -------------------------------------------------------------------------

    [Fact]
    public async Task HandleAsync_AlwaysExecutesInsideSingleTransaction()
    {
        var (handler, _, _, _, _, uow) = BuildHandler();

        await handler.HandleAsync(MakeCommand());

        uow.Verify(u => u.ExecuteInTransactionAsync(
            It.IsAny<Func<CancellationToken, Task>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenJobRepoThrows_PropagatesException()
    {
        var (handler, _, jobRepo, _, _, _) = BuildHandler();
        jobRepo.Setup(r => r.AddAsync(It.IsAny<ImportJob>(), It.IsAny<CancellationToken>()))
               .ThrowsAsync(new InvalidOperationException("DB error"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.HandleAsync(MakeCommand()));
    }
}
