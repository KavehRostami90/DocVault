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
        repository.Setup(r => r.ListAsync(cancellationToken)).ReturnsAsync(tags);

        var handler = new ListTagsHandler(repository.Object);

        var result = await handler.HandleAsync(cancellationToken);

        Assert.Equal(new[] { "alpha", "beta" }, result);
        repository.Verify(r => r.ListAsync(cancellationToken), Times.Once);
        repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Propagates_repository_exception_without_swallowing()
    {
        var cancellationToken = CancellationToken.None;
        var repository = new Mock<ITagRepository>();
        repository.Setup(r => r.ListAsync(cancellationToken)).ThrowsAsync(new InvalidOperationException("boom"));

        var handler = new ListTagsHandler(repository.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.HandleAsync(cancellationToken));
        repository.Verify(r => r.ListAsync(cancellationToken), Times.Once);
        repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Returns_empty_collection_when_repository_is_empty()
    {
        var cancellationToken = CancellationToken.None;
        var repository = new Mock<ITagRepository>();
        repository.Setup(r => r.ListAsync(cancellationToken)).ReturnsAsync(Array.Empty<Tag>());

        var handler = new ListTagsHandler(repository.Object);

        var result = await handler.HandleAsync(cancellationToken);

        Assert.Empty(result);
        repository.Verify(r => r.ListAsync(cancellationToken), Times.Once);
        repository.VerifyNoOtherCalls();
    }

    private static Tag TagNamed(string name) => new(name);
}

