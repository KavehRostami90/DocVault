using DocVault.Domain.Documents;
using DocVault.Infrastructure.Realtime;
using Xunit;

namespace DocVault.UnitTests.Infrastructure.Realtime;

public sealed class DocumentStatusBroadcasterTests
{
    private static DocumentStatusBroadcaster Create() => new();

    // -------------------------------------------------------------------------
    // Subscribe
    // -------------------------------------------------------------------------

    [Fact]
    public void Subscribe_ReturnsNonNullReader()
    {
        var reader = Create().Subscribe(Guid.NewGuid());
        Assert.NotNull(reader);
    }

    // -------------------------------------------------------------------------
    // Publish
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Publish_DeliversEventToSubscriber()
    {
        var broadcaster = Create();
        var docId = Guid.NewGuid();
        var reader = broadcaster.Subscribe(docId);

        broadcaster.Publish(docId, DocumentStatus.Indexed);

        var evt = await reader.ReadAsync();
        Assert.Equal(docId, evt.DocumentId);
        Assert.Equal(DocumentStatus.Indexed, evt.Status);
        Assert.Null(evt.Error);
    }

    [Fact]
    public async Task Publish_WithError_DeliversErrorField()
    {
        var broadcaster = Create();
        var docId = Guid.NewGuid();
        var reader = broadcaster.Subscribe(docId);

        broadcaster.Publish(docId, DocumentStatus.Failed, "something broke");

        var evt = await reader.ReadAsync();
        Assert.Equal(DocumentStatus.Failed, evt.Status);
        Assert.Equal("something broke", evt.Error);
    }

    [Fact]
    public void Publish_NoSubscribers_DoesNotThrow()
    {
        var ex = Record.Exception(() => Create().Publish(Guid.NewGuid(), DocumentStatus.Indexed));
        Assert.Null(ex);
    }

    [Fact]
    public async Task Publish_MultipleSubscribers_SameDocId_AllReceive()
    {
        var broadcaster = Create();
        var docId = Guid.NewGuid();
        var reader1 = broadcaster.Subscribe(docId);
        var reader2 = broadcaster.Subscribe(docId);

        broadcaster.Publish(docId, DocumentStatus.Indexed);

        var evt1 = await reader1.ReadAsync();
        var evt2 = await reader2.ReadAsync();
        Assert.Equal(DocumentStatus.Indexed, evt1.Status);
        Assert.Equal(DocumentStatus.Indexed, evt2.Status);
    }

    [Fact]
    public void Publish_DifferentDocId_DoesNotDeliverToWrongSubscriber()
    {
        var broadcaster = Create();
        var reader = broadcaster.Subscribe(Guid.NewGuid());

        broadcaster.Publish(Guid.NewGuid(), DocumentStatus.Indexed);

        Assert.False(reader.TryRead(out _));
    }

    [Theory]
    [InlineData(DocumentStatus.Imported)]
    [InlineData(DocumentStatus.Indexed)]
    [InlineData(DocumentStatus.Failed)]
    public async Task Publish_PreservesStatusEnum(DocumentStatus status)
    {
        var broadcaster = Create();
        var docId = Guid.NewGuid();
        var reader = broadcaster.Subscribe(docId);

        broadcaster.Publish(docId, status);

        var evt = await reader.ReadAsync();
        Assert.Equal(status, evt.Status);
    }

    [Fact]
    public async Task Publish_MultipleEvents_DeliveredInOrder()
    {
        var broadcaster = Create();
        var docId = Guid.NewGuid();
        var reader = broadcaster.Subscribe(docId);

        broadcaster.Publish(docId, DocumentStatus.Imported);
        broadcaster.Publish(docId, DocumentStatus.Indexed);

        var first  = await reader.ReadAsync();
        var second = await reader.ReadAsync();
        Assert.Equal(DocumentStatus.Imported, first.Status);
        Assert.Equal(DocumentStatus.Indexed, second.Status);
    }

    // -------------------------------------------------------------------------
    // Unsubscribe
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Unsubscribe_CompletesReader_ReadAllAsyncTerminates()
    {
        var broadcaster = Create();
        var docId = Guid.NewGuid();
        var reader = broadcaster.Subscribe(docId);

        broadcaster.Unsubscribe(docId, reader);

        var events = new List<object>();
        await foreach (var evt in reader.ReadAllAsync())
            events.Add(evt);

        Assert.Empty(events);
    }

    [Fact]
    public async Task Unsubscribe_OneOfTwoSubscribers_OtherStillReceives()
    {
        var broadcaster = Create();
        var docId = Guid.NewGuid();
        var reader1 = broadcaster.Subscribe(docId);
        var reader2 = broadcaster.Subscribe(docId);

        broadcaster.Unsubscribe(docId, reader1);
        broadcaster.Publish(docId, DocumentStatus.Indexed);

        Assert.False(reader1.TryRead(out _));
        var evt = await reader2.ReadAsync();
        Assert.Equal(DocumentStatus.Indexed, evt.Status);
    }

    [Fact]
    public void Unsubscribe_NonExistentReader_DoesNotThrow()
    {
        var broadcaster = Create();
        // Reader from a separate broadcaster — not registered in this one.
        var foreignReader = Create().Subscribe(Guid.NewGuid());

        var ex = Record.Exception(() => broadcaster.Unsubscribe(Guid.NewGuid(), foreignReader));
        Assert.Null(ex);
    }

    [Fact]
    public void Unsubscribe_Twice_DoesNotThrow()
    {
        var broadcaster = Create();
        var docId = Guid.NewGuid();
        var reader = broadcaster.Subscribe(docId);

        broadcaster.Unsubscribe(docId, reader);
        var ex = Record.Exception(() => broadcaster.Unsubscribe(docId, reader));
        Assert.Null(ex);
    }

    [Fact]
    public void Unsubscribe_LastSubscriberForDocId_CleansUpSubscriptionEntry()
    {
        var broadcaster = Create();
        var docId = Guid.NewGuid();
        var reader = broadcaster.Subscribe(docId);

        broadcaster.Unsubscribe(docId, reader);

        // After last subscriber is removed, publishing to that docId should be a no-op.
        var ex = Record.Exception(() => broadcaster.Publish(docId, DocumentStatus.Indexed));
        Assert.Null(ex);
    }
}
