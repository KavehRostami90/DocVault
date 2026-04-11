using DocVault.Application.Abstractions.Persistence;
using DocVault.Application.UseCases.Documents.GetDocumentFile;
using DocVault.Domain.Documents;
using DocVault.Domain.Documents.ValueObjects;
using DocVault.Domain.Imports;
using Moq;
using Xunit;

namespace DocVault.UnitTests.Application;

public sealed class GetDocumentFileHandlerTests
{
    private static readonly Guid OwnerId = Guid.NewGuid();

    private static (
        GetDocumentFileHandler Handler,
        Mock<IDocumentRepository> Documents,
        Mock<IImportJobRepository> Imports)
    BuildHandler()
    {
        var documents = new Mock<IDocumentRepository>();
        var imports   = new Mock<IImportJobRepository>();

        var handler = new GetDocumentFileHandler(documents.Object, imports.Object);
        return (handler, documents, imports);
    }

    private static Document CreateDocument(Guid? ownerId = null)
        => new(
            DocumentId.New(),
            "My Doc",
            "doc.txt",
            "text/plain",
            128,
            FileHash.FromBytes([1, 2, 3]),
            ownerId);

    [Fact]
    public async Task HandleAsync_WhenDocumentExists_ReturnsStorageReference()
    {
        var document = CreateDocument(OwnerId);
        var importJob = new ImportJob(Guid.NewGuid(), document.Id, document.FileName, "stored-path.bin", document.ContentType);
        var (handler, documents, imports) = BuildHandler();

        documents.Setup(r => r.GetAsync(document.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);
        imports.Setup(r => r.GetLatestByDocumentIdAsync(document.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(importJob);

        var result = await handler.HandleAsync(new GetDocumentFileQuery(document.Id, OwnerId));

        Assert.True(result.IsSuccess);
        Assert.Equal(document.FileName, result.Value!.FileName);
        Assert.Equal(document.ContentType, result.Value.ContentType);
        Assert.Equal(importJob.StoragePath, result.Value.StoragePath);
    }

    [Fact]
    public async Task HandleAsync_WhenDocumentDoesNotExist_ReturnsNotFound()
    {
        var documentId = DocumentId.New();
        var (handler, documents, _) = BuildHandler();

        documents.Setup(r => r.GetAsync(documentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Document?)null);

        var result = await handler.HandleAsync(new GetDocumentFileQuery(documentId, OwnerId));

        Assert.False(result.IsSuccess);
        Assert.Equal("NotFound", result.Error);
    }

    [Fact]
    public async Task HandleAsync_WhenCallerDoesNotOwnDocument_ReturnsNotFound()
    {
        var document = CreateDocument(Guid.NewGuid());
        var (handler, documents, _) = BuildHandler();

        documents.Setup(r => r.GetAsync(document.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var result = await handler.HandleAsync(new GetDocumentFileQuery(document.Id, OwnerId));

        Assert.False(result.IsSuccess);
        Assert.Equal("NotFound", result.Error);
    }

    [Fact]
    public async Task HandleAsync_WhenLatestImportJobIsMissing_ReturnsNotFound()
    {
        var document = CreateDocument(OwnerId);
        var (handler, documents, imports) = BuildHandler();

        documents.Setup(r => r.GetAsync(document.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);
        imports.Setup(r => r.GetLatestByDocumentIdAsync(document.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ImportJob?)null);

        var result = await handler.HandleAsync(new GetDocumentFileQuery(document.Id, OwnerId));

        Assert.False(result.IsSuccess);
        Assert.Equal("NotFound", result.Error);
    }
}
