using DocVault.Application.Abstractions.Persistence;
using DocVault.Application.UseCases.Tags.ListTags;
using DocVault.Domain.Documents;
using Moq;
using Xunit;

namespace DocVault.UnitTests.Application.Tags;

public class ListTagsHandlerTests
{
    [Fact]
    public async Task Returns_names_in_repository_order()
    {
        var cancellationToken = new CancellationTokenSource().Token;
        var tags = new[] { TagNamed("alpha"), TagNamed("beta") };

        var repository = new Mock<ITagRepository>();
        repository.Setup(r => r.ListAsync(null, cancellationToken)).ReturnsAsync(tags);

        var handler = new ListTagsHandler(repository.Object);

        var result = await handler.HandleAsync(new ListTagsQuery(), cancellationToken);

        Assert.Equal(new[] { "alpha", "beta" }, result);
        repository.Verify(r => r.ListAsync(null, cancellationToken), Times.Once);
        repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Propagates_repository_exception_without_swallowing()
    {
        var cancellationToken = CancellationToken.None;
        var repository = new Mock<ITagRepository>();
        repository.Setup(r => r.ListAsync(null, cancellationToken)).ThrowsAsync(new InvalidOperationException("boom"));

        var handler = new ListTagsHandler(repository.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.HandleAsync(new ListTagsQuery(), cancellationToken));
        repository.Verify(r => r.ListAsync(null, cancellationToken), Times.Once);
        repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Returns_empty_collection_when_repository_is_empty()
    {
        var cancellationToken = CancellationToken.None;
        var repository = new Mock<ITagRepository>();
        repository.Setup(r => r.ListAsync(null, cancellationToken)).ReturnsAsync(Array.Empty<Tag>());

        var handler = new ListTagsHandler(repository.Object);

        var result = await handler.HandleAsync(new ListTagsQuery(), cancellationToken);

        Assert.Empty(result);
        repository.Verify(r => r.ListAsync(null, cancellationToken), Times.Once);
        repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Filters_tags_by_owner_when_owner_id_provided()
    {
        var ownerId = Guid.NewGuid();
        var cancellationToken = CancellationToken.None;
        var tags = new[] { TagNamed("finance") };

        var repository = new Mock<ITagRepository>();
        repository.Setup(r => r.ListAsync(ownerId, cancellationToken)).ReturnsAsync(tags);

        var handler = new ListTagsHandler(repository.Object);

        var result = await handler.HandleAsync(new ListTagsQuery(ownerId), cancellationToken);

        Assert.Equal(new[] { "finance" }, result);
        repository.Verify(r => r.ListAsync(ownerId, cancellationToken), Times.Once);
        repository.VerifyNoOtherCalls();
    }

    private static Tag TagNamed(string name) => new(name);
}

