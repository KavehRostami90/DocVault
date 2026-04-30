using DocVault.Application.Abstractions.Embeddings;
using DocVault.Application.Abstractions.Persistence;
using DocVault.Application.Abstractions.Qa;
using DocVault.Application.Common.Paging;
using DocVault.Application.UseCases.Qa;
using DocVault.Application.UseCases.Search;
using DocVault.Domain.Documents;
using DocVault.Domain.Documents.ValueObjects;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DocVault.UnitTests.Application.Qa;

public sealed class AskQuestionHandlerTests
{
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static Document MakeIndexedDocument(DocumentId id, string title, string text, Guid? ownerId = null)
    {
        var doc = new Document(id, title, "file.txt", "text/plain", 100, new FileHash("abc"), ownerId);
        doc.MarkImported();
        doc.AttachText(text);
        doc.MarkIndexed();
        return doc;
    }

    private static Page<SearchResultItem> MakeSearchPage(params (Document doc, double score)[] items)
    {
        var resultItems = items
            .Select(x => new SearchResultItem(
                new DocumentSearchSummary(x.doc.Id, x.doc.Title, x.doc.Text ?? string.Empty),
                x.score))
            .ToList();
        return new Page<SearchResultItem>(resultItems, 1, 8, resultItems.Count);
    }

    private static (
        AskQuestionHandler Handler,
        Mock<IDocumentRepository> DocRepo,
        Mock<IQuestionAnsweringService> QaService)
    BuildHandler()
    {
        var docRepo                = new Mock<IDocumentRepository>();
        var embeddingProvider      = new Mock<IEmbeddingProvider>();
        var searchHandlerLogger    = new Mock<ILogger<SearchDocumentsHandler>>();
        var askQuestionHandlerLogger = new Mock<ILogger<AskQuestionHandler>>();

        // Embedding provider returns a dummy vector so the semantic-search path is exercised.
        embeddingProvider
            .Setup(e => e.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[768]);

        var searchHandler = new SearchDocumentsHandler(docRepo.Object, embeddingProvider.Object, searchHandlerLogger.Object);
        var qaService     = new Mock<IQuestionAnsweringService>();
        var handler       = new AskQuestionHandler(searchHandler, qaService.Object, askQuestionHandlerLogger.Object);

        return (handler, docRepo, qaService);
    }

    // -------------------------------------------------------------------------
    // Empty search results
    // -------------------------------------------------------------------------

    [Fact]
    public async Task HandleAsync_EmptySearchResults_ReturnsSuccessWithNoResultsMessage()
    {
        var (handler, docRepo, _) = BuildHandler();

        docRepo.Setup(r => r.SearchAsync(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<Guid?>(), It.IsAny<float[]?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Page<SearchResultItem>([], 1, 8, 0));

        var result = await handler.HandleAsync(new AskQuestionQuery("What is this?"));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.False(result.Value.AnsweredByModel);
        Assert.Contains("couldn't find", result.Value.Answer, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HandleAsync_EmptySearchResults_NeverCallsQaService()
    {
        var (handler, docRepo, qaService) = BuildHandler();

        docRepo.Setup(r => r.SearchAsync(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<Guid?>(), It.IsAny<float[]?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Page<SearchResultItem>([], 1, 8, 0));

        await handler.HandleAsync(new AskQuestionQuery("What is this?"));

        qaService.Verify(q => q.AnswerAsync(
            It.IsAny<string>(), It.IsAny<IReadOnlyList<QaContextChunk>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_SearchReturnsDocumentsWithEmptyText_ReturnsNoResultsMessage()
    {
        var (handler, docRepo, qaService) = BuildHandler();

        // Document exists but has no extractable text (e.g. empty file or OCR yielded nothing).
        var doc = new Document(DocumentId.New(), "Empty Doc", "empty.txt", "text/plain", 100, new FileHash("abc"));
        doc.MarkImported();
        doc.AttachText(string.Empty);
        doc.MarkIndexed();

        docRepo.Setup(r => r.SearchAsync(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<Guid?>(), It.IsAny<float[]?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeSearchPage((doc, 0.9)));

        var result = await handler.HandleAsync(new AskQuestionQuery("What is this?"));

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.AnsweredByModel);
        qaService.Verify(q => q.AnswerAsync(
            It.IsAny<string>(), It.IsAny<IReadOnlyList<QaContextChunk>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    // -------------------------------------------------------------------------
    // DocumentId filtering
    // -------------------------------------------------------------------------

    [Fact]
    public async Task HandleAsync_WithDocumentIdFilter_OnlyPassesMatchingDocumentChunksToQa()
    {
        var (handler, docRepo, qaService) = BuildHandler();

        var targetId  = DocumentId.New();
        var otherId   = DocumentId.New();
        var targetDoc = MakeIndexedDocument(targetId, "Target", "Target document content with useful indexed text.");
        var otherDoc  = MakeIndexedDocument(otherId,  "Other",  "Other document content that should be excluded.");

        docRepo.Setup(r => r.SearchAsync(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<Guid?>(), It.IsAny<float[]?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeSearchPage((targetDoc, 0.9), (otherDoc, 0.8)));

        IReadOnlyList<QaContextChunk>? capturedContexts = null;
        qaService
            .Setup(q => q.AnswerAsync(
                It.IsAny<string>(), It.IsAny<IReadOnlyList<QaContextChunk>>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, IReadOnlyList<QaContextChunk>, CancellationToken>((_, ctxs, _) => capturedContexts = ctxs)
            .ReturnsAsync(new QaAnswerResult("Answer from target", true));

        var result = await handler.HandleAsync(new AskQuestionQuery("What is this?", DocumentId: targetId.Value));

        Assert.True(result.IsSuccess);
        Assert.NotNull(capturedContexts);
        Assert.All(capturedContexts, c => Assert.Equal(targetId.Value, c.DocumentId));
        Assert.DoesNotContain(capturedContexts, c => c.DocumentId == otherId.Value);
    }

    [Fact]
    public async Task HandleAsync_DocumentIdFilterMatchesNoResults_ReturnsNoResultsMessage()
    {
        var (handler, docRepo, qaService) = BuildHandler();

        var existingId  = DocumentId.New();
        var filteredId  = Guid.NewGuid();           // not in the search results
        var existingDoc = MakeIndexedDocument(existingId, "Some Doc", "Some text content.");

        docRepo.Setup(r => r.SearchAsync(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<Guid?>(), It.IsAny<float[]?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeSearchPage((existingDoc, 0.9)));

        var result = await handler.HandleAsync(new AskQuestionQuery("What is this?", DocumentId: filteredId));

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.AnsweredByModel);
        qaService.Verify(q => q.AnswerAsync(
            It.IsAny<string>(), It.IsAny<IReadOnlyList<QaContextChunk>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    // -------------------------------------------------------------------------
    // Chunk scoring / ordering
    // -------------------------------------------------------------------------

    [Fact]
    public async Task HandleAsync_ChunksPassedToQaAreOrderedByDescendingScore()
    {
        var (handler, docRepo, qaService) = BuildHandler();

        // Two docs with the same text; the second has a higher semantic score.
        var lowScoreDoc  = MakeIndexedDocument(DocumentId.New(), "Low",  "banana fruit information details context.");
        var highScoreDoc = MakeIndexedDocument(DocumentId.New(), "High", "banana fruit information details context.");

        docRepo.Setup(r => r.SearchAsync(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<Guid?>(), It.IsAny<float[]?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeSearchPage((lowScoreDoc, 0.3), (highScoreDoc, 0.95)));

        IReadOnlyList<QaContextChunk>? capturedContexts = null;
        qaService
            .Setup(q => q.AnswerAsync(
                It.IsAny<string>(), It.IsAny<IReadOnlyList<QaContextChunk>>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, IReadOnlyList<QaContextChunk>, CancellationToken>((_, ctxs, _) => capturedContexts = ctxs)
            .ReturnsAsync(new QaAnswerResult("Answer", true));

        await handler.HandleAsync(new AskQuestionQuery("banana fruit", MaxContexts: 10));

        Assert.NotNull(capturedContexts);
        Assert.True(capturedContexts.Count > 1, "Expected more than one context chunk.");

        // Verify descending order.
        for (var i = 0; i < capturedContexts.Count - 1; i++)
        {
            Assert.True(
                capturedContexts[i].RetrievalScore >= capturedContexts[i + 1].RetrievalScore,
                $"Chunk {i} (score {capturedContexts[i].RetrievalScore}) should be >= chunk {i + 1} (score {capturedContexts[i + 1].RetrievalScore}).");
        }
    }

    [Fact]
    public async Task HandleAsync_MaxContextsLimitsChunksPassedToQa()
    {
        var (handler, docRepo, qaService) = BuildHandler();

        // 500 words × ~5 chars each ≈ 2 500 characters; with a 700-char window this
        // produces 4+ chunks, ensuring MaxContexts trimming is actually exercised.
        const int WordCount  = 500;
        var longText = string.Join(" ", Enumerable.Repeat("word", WordCount));
        var doc      = MakeIndexedDocument(DocumentId.New(), "Big Doc", longText);

        docRepo.Setup(r => r.SearchAsync(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<Guid?>(), It.IsAny<float[]?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeSearchPage((doc, 0.9)));

        IReadOnlyList<QaContextChunk>? capturedContexts = null;
        qaService
            .Setup(q => q.AnswerAsync(
                It.IsAny<string>(), It.IsAny<IReadOnlyList<QaContextChunk>>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, IReadOnlyList<QaContextChunk>, CancellationToken>((_, ctxs, _) => capturedContexts = ctxs)
            .ReturnsAsync(new QaAnswerResult("Answer", true));

        // 2 is less than the number of chunks produced by WordCount words, so the
        // limiting behaviour is actually exercised.
        const int maxContexts = 2;
        await handler.HandleAsync(new AskQuestionQuery("word content", MaxContexts: maxContexts));

        Assert.NotNull(capturedContexts);
        Assert.True(
            capturedContexts.Count <= maxContexts,
            $"Expected at most {maxContexts} context chunks but got {capturedContexts.Count}.");
    }

    // -------------------------------------------------------------------------
    // Provider exception handling
    // -------------------------------------------------------------------------

    [Fact]
    public async Task HandleAsync_WhenQaServiceThrows_ReturnsFailure()
    {
        var (handler, docRepo, qaService) = BuildHandler();

        var doc = MakeIndexedDocument(DocumentId.New(), "Doc", "Some indexed text content for testing purposes.");

        docRepo.Setup(r => r.SearchAsync(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<Guid?>(), It.IsAny<float[]?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeSearchPage((doc, 0.9)));

        qaService
            .Setup(q => q.AnswerAsync(
                It.IsAny<string>(), It.IsAny<IReadOnlyList<QaContextChunk>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Connection refused http://internal-service:5000"));

        var result = await handler.HandleAsync(new AskQuestionQuery("What is this?"));

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public async Task HandleAsync_WhenQaServiceThrows_ErrorMessageDoesNotLeakInternalDetails()
    {
        var (handler, docRepo, qaService) = BuildHandler();

        var doc = MakeIndexedDocument(DocumentId.New(), "Doc", "Some indexed text content for testing purposes.");

        docRepo.Setup(r => r.SearchAsync(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<Guid?>(), It.IsAny<float[]?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeSearchPage((doc, 0.9)));

        qaService
            .Setup(q => q.AnswerAsync(
                It.IsAny<string>(), It.IsAny<IReadOnlyList<QaContextChunk>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Connection refused http://internal-service:5000"));

        var result = await handler.HandleAsync(new AskQuestionQuery("What is this?"));

        // The error surfaced to the caller must not contain raw exception details or internal URLs.
        Assert.DoesNotContain("http://internal-service:5000", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Connection refused", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    // -------------------------------------------------------------------------
    // Success path
    // -------------------------------------------------------------------------

    [Fact]
    public async Task HandleAsync_WithIndexedDocumentAndAnswer_ReturnsSuccessWithCitations()
    {
        var (handler, docRepo, qaService) = BuildHandler();

        var docId = DocumentId.New();
        var doc   = MakeIndexedDocument(docId, "Research Paper", "Important findings about topic X that are relevant.");

        docRepo.Setup(r => r.SearchAsync(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<Guid?>(), It.IsAny<float[]?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeSearchPage((doc, 0.85)));

        qaService
            .Setup(q => q.AnswerAsync(
                It.IsAny<string>(), It.IsAny<IReadOnlyList<QaContextChunk>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QaAnswerResult("The answer is X.", true));

        var result = await handler.HandleAsync(new AskQuestionQuery("What are the findings?"));

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.AnsweredByModel);
        Assert.Equal("The answer is X.", result.Value.Answer);
        Assert.NotEmpty(result.Value.Citations);
        Assert.All(result.Value.Citations, c => Assert.Equal(docId.Value, c.DocumentId));
    }
}
