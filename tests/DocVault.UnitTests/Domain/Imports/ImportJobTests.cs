using DocVault.Domain.Documents;
using DocVault.Domain.Imports;
using DocVault.Domain.Primitives;
using Xunit;

namespace DocVault.UnitTests.Domain.Imports;

public sealed class ImportJobTests
{
    // -------------------------------------------------------------------------
    // Factory helpers
    // -------------------------------------------------------------------------

    private static DocumentId NewDocumentId() => DocumentId.New();
    private const string ValidFileName    = "report.pdf";
    private const string ValidStoragePath = "/storage/report.bin";
    private const string ValidContentType = "application/pdf";

    private static ImportJob MakeJob(
        DocumentId? documentId = null,
        string fileName    = ValidFileName,
        string storagePath = ValidStoragePath,
        string contentType = ValidContentType)
    {
        var docId = documentId ?? NewDocumentId();
        return new ImportJob(Guid.NewGuid(), docId, fileName, storagePath, contentType);
    }

    // -------------------------------------------------------------------------
    // Constructor — success
    // -------------------------------------------------------------------------

    [Fact]
    public void Constructor_ValidArgs_SetsPendingStatus()
    {
        var job = MakeJob();
        Assert.Equal(ImportStatus.Pending, job.Status);
    }

    [Fact]
    public void Constructor_ValidArgs_SetsDocumentId()
    {
        var docId = NewDocumentId();
        var job = MakeJob(documentId: docId);
        Assert.Equal(docId, job.DocumentId);
    }

    [Fact]
    public void Constructor_ValidArgs_SetsFileName()
    {
        var job = MakeJob(fileName: "invoice.pdf");
        Assert.Equal("invoice.pdf", job.FileName);
    }

    [Fact]
    public void Constructor_ValidArgs_SetsStoragePath()
    {
        var job = MakeJob(storagePath: "/bucket/data.bin");
        Assert.Equal("/bucket/data.bin", job.StoragePath);
    }

    [Fact]
    public void Constructor_ValidArgs_SetsContentType()
    {
        var job = MakeJob(contentType: "text/plain");
        Assert.Equal("text/plain", job.ContentType);
    }

    [Fact]
    public void Constructor_ValidArgs_SetsStartedAt_ToNowApproximately()
    {
        var before = DateTime.UtcNow;
        var job = MakeJob();
        var after = DateTime.UtcNow;
        Assert.InRange(job.StartedAt, before, after);
    }

    [Fact]
    public void Constructor_ValidArgs_CompletedAt_IsNull()
    {
        var job = MakeJob();
        Assert.Null(job.CompletedAt);
    }

    // -------------------------------------------------------------------------
    // Constructor — validation guards
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_EmptyFileName_ThrowsDomainException(string fileName)
    {
        Assert.Throws<DomainException>(() => MakeJob(fileName: fileName));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_EmptyStoragePath_ThrowsDomainException(string storagePath)
    {
        Assert.Throws<DomainException>(() => MakeJob(storagePath: storagePath));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_EmptyContentType_ThrowsDomainException(string contentType)
    {
        Assert.Throws<DomainException>(() => MakeJob(contentType: contentType));
    }

    // -------------------------------------------------------------------------
    // MarkInProgress
    // -------------------------------------------------------------------------

    [Fact]
    public void MarkInProgress_SetsStatusToInProgress()
    {
        var job = MakeJob();
        job.MarkInProgress();
        Assert.Equal(ImportStatus.InProgress, job.Status);
    }

    [Fact]
    public void MarkInProgress_DoesNotSetCompletedAt()
    {
        var job = MakeJob();
        job.MarkInProgress();
        Assert.Null(job.CompletedAt);
    }

    // -------------------------------------------------------------------------
    // MarkCompleted
    // -------------------------------------------------------------------------

    [Fact]
    public void MarkCompleted_SetsStatusToCompleted()
    {
        var job = MakeJob();
        job.MarkInProgress();
        job.MarkCompleted();
        Assert.Equal(ImportStatus.Completed, job.Status);
    }

    [Fact]
    public void MarkCompleted_SetsCompletedAt()
    {
        var before = DateTime.UtcNow;
        var job = MakeJob();
        job.MarkInProgress();
        job.MarkCompleted();
        var after = DateTime.UtcNow;
        Assert.NotNull(job.CompletedAt);
        Assert.InRange(job.CompletedAt!.Value, before, after);
    }

    // -------------------------------------------------------------------------
    // MarkFailed
    // -------------------------------------------------------------------------

    [Fact]
    public void MarkFailed_SetsStatusToFailed()
    {
        var job = MakeJob();
        job.MarkInProgress();
        job.MarkFailed("pipeline error");
        Assert.Equal(ImportStatus.Failed, job.Status);
    }

    [Fact]
    public void MarkFailed_StoresErrorMessage()
    {
        var job = MakeJob();
        job.MarkFailed("pipeline error");
        Assert.Equal("pipeline error", job.Error);
    }

    [Fact]
    public void MarkFailed_SetsCompletedAt()
    {
        var before = DateTime.UtcNow;
        var job = MakeJob();
        job.MarkFailed("error");
        var after = DateTime.UtcNow;
        Assert.NotNull(job.CompletedAt);
        Assert.InRange(job.CompletedAt!.Value, before, after);
    }

    // -------------------------------------------------------------------------
    // Full lifecycle
    // -------------------------------------------------------------------------

    [Fact]
    public void FullLifecycle_PendingToInProgressToCompleted()
    {
        var job = MakeJob();
        Assert.Equal(ImportStatus.Pending, job.Status);

        job.MarkInProgress();
        Assert.Equal(ImportStatus.InProgress, job.Status);

        job.MarkCompleted();
        Assert.Equal(ImportStatus.Completed, job.Status);
        Assert.NotNull(job.CompletedAt);
        Assert.Null(job.Error);
    }

    [Fact]
    public void FullLifecycle_PendingToInProgressToFailed()
    {
        const string errorMsg = "extraction failed";
        var job = MakeJob();

        job.MarkInProgress();
        job.MarkFailed(errorMsg);

        Assert.Equal(ImportStatus.Failed, job.Status);
        Assert.Equal(errorMsg, job.Error);
        Assert.NotNull(job.CompletedAt);
    }
}

