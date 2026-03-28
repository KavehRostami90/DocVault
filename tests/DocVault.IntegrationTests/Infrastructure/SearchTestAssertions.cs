using Xunit;

namespace DocVault.IntegrationTests.Infrastructure;

/// <summary>
/// Implementation of search test assertions.
/// Follows Single Responsibility Principle - only handles search-related assertions.
/// </summary>
public sealed class SearchTestAssertions : ISearchTestAssertions
{
    public void AssertSearchResult(SearchPage page, int expectedCount, Guid expectedFirstDocumentId)
    {
        Assert.NotNull(page);
        Assert.Equal(expectedCount, page.TotalCount);
        
        if (expectedCount > 0)
        {
            Assert.NotEmpty(page.Items);
            Assert.Equal(expectedFirstDocumentId, page.Items[0].Id);
        }
    }

    public void AssertEmptySearchResult(SearchPage page)
    {
        Assert.NotNull(page);
        Assert.Equal(0, page.TotalCount);
        Assert.Empty(page.Items);
    }

    public void AssertPaginationCorrect(SearchPage page, int expectedPage, int expectedSize, long expectedTotal)
    {
        Assert.NotNull(page);
        Assert.Equal(expectedPage, page.Page);
        Assert.Equal(expectedSize, page.Size);
        Assert.Equal(expectedTotal, page.TotalCount);
    }

    public void AssertScoreInRange(SearchPage page, double minScore = 0.0, double maxScore = 1.0)
    {
        Assert.NotNull(page);
        Assert.All(page.Items, item =>
        {
            Assert.True(item.Score >= minScore && item.Score <= maxScore,
                $"Score {item.Score} for '{item.Title}' is outside [{minScore}, {maxScore}]");
        });
    }
}

