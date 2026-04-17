using DocVault.Application.Abstractions.Persistence;
using DocVault.Application.Background;
using DocVault.Application.Background.Queue;
using DocVault.Application.Pipeline;
using DocVault.Domain.Documents;
using DocVault.Domain.Documents.ValueObjects;
using DocVault.Domain.Imports;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace DocVault.UnitTests.Application.IndexingWorker;

/// <summary>
/// Tests for <see cref="DocVault.Application.Background.IndexingWorker"/>.
/// Uses real <see cref="Document"/> and <see cref="ImportJob"/> aggregate instances
/// and mocks all infrastructure ports.
/// </summary>
public sealed class IndexingWorkerTests
{
    // -------------------------------------------------------------------------
    // Shared test data helpers
    // -------------------------------------------------------------------------

    private static Document MakeDocument(DocumentId id)
    {
        var doc = new Document(id, "Test Document", "test.txt", "text/plain", 100,
                               new FileHash("abc123"));
        doc.MarkImported(); // worker always receives documents already in Imported state
        return doc;
    }

    private static ImportJob MakeJob(DocumentId documentId, Guid? jobId = null)
        => new(jobId ?? Guid.NewGuid(), documentId,
               "test.txt", "/storage/test.bin", "text/plain");

    // -------------------------------------------------------------------------
    // Mock-based worker factory
    // -------------------------------------------------------------------------

    private static (
        DocVault.Application.Background.IndexingWorker Worker,
        Mock<IWorkQueue<IndexingWorkItem>> Queue,
        Mock<IIngestionPipeline> Pipeline,
        Mock<IImportJobRepository> JobRepo,
        Mock<IDocumentRepository> DocRepo)
    BuildWorker(Action<Mock<IImportJobRepository>>? configureJobRepo = null,
                Action<Mock<IDocumentRepository>>? configureDocRepo  = null)
    {
        var queue    = new Mock<IWorkQueue<IndexingWorkItem>>();
        var pipeline = new Mock<IIngestionPipeline>();
        var jobRepo  = new Mock<IImportJobRepository>();
        var docRepo  = new Mock<IDocumentRepository>();

        // Default: no pending jobs to recover
        jobRepo.Setup(r => r.GetPendingAsync(It.IsAny<CancellationToken>()))
               .ReturnsAsync(Array.Empty<ImportJob>());

        configureJobRepo?.Invoke(jobRepo);
        configureDocRepo?.Invoke(docRepo);

        // ServiceProvider that resolves the two repos
        var services = new ServiceCollection();
        services.AddSingleton(jobRepo.Object);
        services.AddSingleton(docRepo.Object);
        var sp = services.BuildServiceProvider();

        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory.Setup(f => f.CreateScope()).Returns(() =>
        {
            var scope = new Mock<IServiceScope>();
            scope.SetupGet(s => s.ServiceProvider).Returns(sp);
            return scope.Object;
        });

        var worker = new DocVault.Application.Background.IndexingWorker(
            queue.Object,
            pipeline.Object,
            scopeFactory.Object,
            NullLogger<DocVault.Application.Background.IndexingWorker>.Instance);

        return (worker, queue, pipeline, jobRepo, docRepo);
    }

    /// <summary>
    /// Sets up the queue to return <paramref name="item"/> once, then block until
    /// the <paramref name="cts"/> is cancelled (simulating no more work).
    /// </summary>
    private static void SetupQueueSingleItem(
        Mock<IWorkQueue<IndexingWorkItem>> queue,
        IndexingWorkItem item,
        CancellationTokenSource cts)
    {
        var callCount = 0;
        queue.Setup(q => q.DequeueAsync(It.IsAny<CancellationToken>()))
             .Returns<CancellationToken>(async ct =>
             {
                 if (callCount++ == 0)
                     return item;

                 // Block (and let StopAsync cancel us) so the loop exits cleanly.
                 await Task.Delay(Timeout.Infinite, ct);
                 return item; // unreachable
             });
    }

    // -------------------------------------------------------------------------
    // Happy path — document gets indexed
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ProcessItem_PipelineSucceeds_MarksJobCompletedAndDocumentIndexed()
    {
        var documentId = DocumentId.New();
        var document   = MakeDocument(documentId);
        var job        = MakeJob(documentId);
        var workItem   = new IndexingWorkItem(job.Id, job.StoragePath, job.ContentType);

        var (worker, queue, pipeline, jobRepo, docRepo) = BuildWorker(
            jobRepo => jobRepo.Setup(r => r.GetAsync(job.Id, It.IsAny<CancellationToken>()))
                              .ReturnsAsync(job),
            docRepo => docRepo.Setup(r => r.GetAsync(documentId, It.IsAny<CancellationToken>()))
                              .ReturnsAsync(document));

        pipeline.Setup(p => p.RunAsync(workItem.StoragePath, workItem.ContentType, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new IngestionResult("extracted text content", new float[128]));

        // Signal when we know processing is done (doc is updated)
        var processingDone = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        docRepo.Setup(r => r.UpdateAsync(It.IsAny<Document>(), It.IsAny<CancellationToken>()))
               .Callback<Document, CancellationToken>((_, _) => processingDone.TrySetResult())
               .Returns(Task.CompletedTask);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        SetupQueueSingleItem(queue, workItem, cts);

        await worker.StartAsync(CancellationToken.None);
        await processingDone.Task.WaitAsync(cts.Token);
        await worker.StopAsync(CancellationToken.None);

        // Document should be indexed with extracted text
        Assert.Equal(DocumentStatus.Indexed, document.Status);
        Assert.Equal("extracted text content", document.Text);

        // Job should be completed
        Assert.Equal(ImportStatus.Completed, job.Status);
        Assert.NotNull(job.CompletedAt);
    }

    // -------------------------------------------------------------------------
    // Empty-text path — OCR produces no text → document indexed without embedding
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ProcessItem_PipelineReturnsEmptyText_MarksDocumentIndexedWithNoEmbedding()
    {
        var documentId = DocumentId.New();
        var document   = MakeDocument(documentId);
        var job        = MakeJob(documentId);
        var workItem   = new IndexingWorkItem(job.Id, job.StoragePath, job.ContentType);

        var (worker, queue, pipeline, jobRepo, docRepo) = BuildWorker(
            jobRepo => jobRepo.Setup(r => r.GetAsync(job.Id, It.IsAny<CancellationToken>()))
                              .ReturnsAsync(job),
            docRepo => docRepo.Setup(r => r.GetAsync(documentId, It.IsAny<CancellationToken>()))
                              .ReturnsAsync(document));

        // Pipeline returns empty text (e.g. OCR produced no output) with null embedding
        pipeline.Setup(p => p.RunAsync(workItem.StoragePath, workItem.ContentType, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new IngestionResult(string.Empty, null));

        var processingDone = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        docRepo.Setup(r => r.UpdateAsync(It.IsAny<Document>(), It.IsAny<CancellationToken>()))
               .Callback<Document, CancellationToken>((_, _) => processingDone.TrySetResult())
               .Returns(Task.CompletedTask);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        SetupQueueSingleItem(queue, workItem, cts);

        await worker.StartAsync(CancellationToken.None);
        await processingDone.Task.WaitAsync(cts.Token);
        await worker.StopAsync(CancellationToken.None);

        // Document should be indexed with empty text and no embedding (OCR failed silently)
        Assert.Equal(DocumentStatus.Indexed, document.Status);
        Assert.Equal(string.Empty, document.Text);
        Assert.Null(document.Embedding);

        // Job should still be completed — OCR failure is not a pipeline error
        Assert.Equal(ImportStatus.Completed, job.Status);
    }

    // -------------------------------------------------------------------------
    // Failure path — pipeline throws → marks both job and document as failed
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ProcessItem_PipelineThrows_MarksJobAndDocumentFailed()
    {
        var documentId = DocumentId.New();
        var document   = MakeDocument(documentId);
        var job        = MakeJob(documentId);
        var workItem   = new IndexingWorkItem(job.Id, job.StoragePath, job.ContentType);

        var (worker, queue, pipeline, jobRepo, docRepo) = BuildWorker(
            jobRepo => jobRepo.Setup(r => r.GetAsync(job.Id, It.IsAny<CancellationToken>()))
                              .ReturnsAsync(job),
            docRepo => docRepo.Setup(r => r.GetAsync(documentId, It.IsAny<CancellationToken>()))
                              .ReturnsAsync(document));

        pipeline.Setup(p => p.RunAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("extraction failed"));


        // Signal when we know failure handling is done (doc UpdateAsync is called)
        var failureDone = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        docRepo.Setup(r => r.UpdateAsync(It.IsAny<Document>(), It.IsAny<CancellationToken>()))
               .Callback<Document, CancellationToken>((_, _) => failureDone.TrySetResult())
               .Returns(Task.CompletedTask);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        SetupQueueSingleItem(queue, workItem, cts);

        await worker.StartAsync(CancellationToken.None);
        await failureDone.Task.WaitAsync(cts.Token);
        await worker.StopAsync(CancellationToken.None);

        // Job and document should be failed
        Assert.Equal(ImportStatus.Failed, job.Status);
        Assert.Equal("extraction failed", job.Error);

        Assert.Equal(DocumentStatus.Failed, document.Status);
        Assert.Equal("extraction failed", document.IndexingError);
    }

    // -------------------------------------------------------------------------
    // Job not found — skips silently
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ProcessItem_JobNotFound_SkipsWithoutException()
    {
        var unknownJobId = Guid.NewGuid();
        var workItem = new IndexingWorkItem(unknownJobId, "/x", "text/plain");

        var (worker, queue, pipeline, jobRepo, _) = BuildWorker(
            jobRepo => jobRepo.Setup(r => r.GetAsync(unknownJobId, It.IsAny<CancellationToken>()))
                              .ReturnsAsync((ImportJob?)null));

        // Signal that DequeueAsync was called at least twice (item processed + waiting for next)
        var secondDequeue = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var callCount = 0;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        queue.Setup(q => q.DequeueAsync(It.IsAny<CancellationToken>()))
             .Returns<CancellationToken>(async ct =>
             {
                 var n = Interlocked.Increment(ref callCount);
                 if (n == 1)
                     return workItem;

                 secondDequeue.TrySetResult();
                 await Task.Delay(Timeout.Infinite, ct);
                 return workItem;
             });

        await worker.StartAsync(CancellationToken.None);
        await secondDequeue.Task.WaitAsync(cts.Token); // worker looped back → processed without crashing
        await worker.StopAsync(CancellationToken.None);

        // Pipeline should never have been called because the job was not found
        pipeline.Verify(p => p.RunAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
                        Times.Never);
    }

    // -------------------------------------------------------------------------
    // Recovery on startup — pending jobs are re-enqueued
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Startup_PendingJobsExist_ReEnqueuesAll()
    {
        var docId1 = DocumentId.New();
        var docId2 = DocumentId.New();
        var job1   = MakeJob(docId1, Guid.NewGuid());
        var job2   = MakeJob(docId2, Guid.NewGuid());

        var (worker, queue, _, jobRepo, _) = BuildWorker(
            jobRepo => jobRepo.Setup(r => r.GetPendingAsync(It.IsAny<CancellationToken>()))
                              .ReturnsAsync(new[] { job1, job2 }));

        // Block immediately so we only test the recovery path without processing
        using var cts = new CancellationTokenSource();
        queue.Setup(q => q.DequeueAsync(It.IsAny<CancellationToken>()))
             .Returns<CancellationToken>(async ct =>
             {
                 await Task.Delay(Timeout.Infinite, ct);
                 return default!;
             });

        await worker.StartAsync(CancellationToken.None);
        // Give the recover path a moment to run before stopping
        await Task.Delay(100);
        await worker.StopAsync(CancellationToken.None);

        queue.Verify(q => q.Enqueue(It.Is<IndexingWorkItem>(w => w.JobId == job1.Id)), Times.Once);
        queue.Verify(q => q.Enqueue(It.Is<IndexingWorkItem>(w => w.JobId == job2.Id)), Times.Once);
    }

    // -------------------------------------------------------------------------
    // Recovery — no pending jobs means nothing enqueued
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Startup_NoPendingJobs_EnqueuesNothing()
    {
        var (worker, queue, _, _, _) = BuildWorker(); // default: empty pending list

        using var cts = new CancellationTokenSource();
        queue.Setup(q => q.DequeueAsync(It.IsAny<CancellationToken>()))
             .Returns<CancellationToken>(async ct =>
             {
                 await Task.Delay(Timeout.Infinite, ct);
                 return default!;
             });

        await worker.StartAsync(CancellationToken.None);
        await Task.Delay(100);
        await worker.StopAsync(CancellationToken.None);

        queue.Verify(q => q.Enqueue(It.IsAny<IndexingWorkItem>()), Times.Never);
    }
}

