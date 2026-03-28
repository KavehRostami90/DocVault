namespace DocVault.IntegrationTests.Infrastructure;

/// <summary>
/// Interface for test assertion helpers.
/// Follows ISP - specific assertion interfaces for different test types.
/// </summary>
public interface ISearchTestAssertions
{
    void AssertSearchResult(SearchPage page, int expectedCount, Guid expectedFirstDocumentId);
    void AssertEmptySearchResult(SearchPage page);
    void AssertPaginationCorrect(SearchPage page, int expectedPage, int expectedSize, long expectedTotal);
    void AssertScoreInRange(SearchPage page, double minScore = 0.0, double maxScore = 1.0);
}

/// <summary>
/// Interface for document test assertions.
/// </summary>
public interface IDocumentTestAssertions
{
    void AssertDocumentExists(Guid documentId);
    void AssertDocumentHasTitle(Guid documentId, string expectedTitle);
    void AssertDocumentHasContent(Guid documentId, string expectedContent);
}

