using DocVault.IntegrationTests.Infrastructure;
using Xunit;

namespace DocVault.IntegrationTests.Api.Search;

/// <summary>
/// Tests for search pagination functionality.
/// Follows Single Responsibility Principle - only tests pagination behavior.
/// </summary>
public sealed class SearchPaginationTests : BaseIntegrationTest
{
    private readonly List<Guid> _documentIds = new();

    public SearchPaginationTests(DocVaultFactory factory) : base(factory)
    {
    }

    protected override async Task SeedTestDataAsync()
    {
        // Seed multiple documents that will match our shared term
        var documents = new[]
        {
            ($"{SearchTestConstants.SHARED_TERM} Document 1", $"Content with {SearchTestConstants.SHARED_TERM}"),
            ($"{SearchTestConstants.SHARED_TERM} Document 2", $"Content with {SearchTestConstants.SHARED_TERM}"),
            ($"Document 3 {SearchTestConstants.SHARED_TERM}", $"Content with {SearchTestConstants.SHARED_TERM}"),
            ($"Document 4", $"Content with {SearchTestConstants.SHARED_TERM}"),
            ($"Document 5", $"Content with {SearchTestConstants.SHARED_TERM}")
        };

        var ids = await DataSeeder.SeedMultipleDocumentsAsync(documents);
        _documentIds.AddRange(ids);
    }

    [Fact]
    public async Task Search_TotalCount_ReflectsCorpusNotPageSize()
    {
        // Act - Request page 1 with size 1, but expect total to reflect all matches
        var page = await SearchHelpers.ExecuteSearchAsync(SearchTestConstants.SHARED_TERM, page: 1, size: 1);

        // Assert
        SearchAssertions.AssertPaginationCorrect(page, expectedPage: 1, expectedSize: 1, expectedTotal: 5);
        Assert.Single(page.Items);
    }

    [Fact]
    public async Task Search_SecondPage_ReturnsDistinctItem()
    {
        // Act
        var page1 = await SearchHelpers.ExecuteSearchAsync(SearchTestConstants.SHARED_TERM, page: 1, size: 1);
        var page2 = await SearchHelpers.ExecuteSearchAsync(SearchTestConstants.SHARED_TERM, page: 2, size: 1);

        // Assert
        Assert.Single(page1.Items);
        Assert.Single(page2.Items);
        Assert.NotEqual(page1.Items[0].Id, page2.Items[0].Id);
    }

    [Fact]
    public async Task Search_PageBeyondResults_ReturnsEmptyItemsButCorrectTotal()
    {
        // Act
        var page = await SearchHelpers.ExecuteSearchAsync(SearchTestConstants.SHARED_TERM, page: 999, size: 10);

        // Assert
        SearchAssertions.AssertPaginationCorrect(page, expectedPage: 999, expectedSize: 10, expectedTotal: 5);
        Assert.Empty(page.Items);
    }

    [Fact]
    public async Task Search_LargePageSize_ReturnsAllResults()
    {
        // Act
        var page = await SearchHelpers.ExecuteSearchAsync(SearchTestConstants.SHARED_TERM, page: 1, size: 100);

        // Assert
        SearchAssertions.AssertPaginationCorrect(page, expectedPage: 1, expectedSize: 100, expectedTotal: 5);
        Assert.Equal(5, page.Items.Count);
    }
}
