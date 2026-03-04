using DocVault.IntegrationTests.Infrastructure;
using Xunit;

namespace DocVault.IntegrationTests.Api.Search;

/// <summary>
/// Tests for search scoring and ranking functionality.
/// Follows Single Responsibility Principle - only tests scoring behavior.
/// </summary>
public sealed class SearchScoringTests : BaseIntegrationTest
{
    private Guid _titleAndTextDocumentId;
    private Guid _textOnlyDocumentId;

    public SearchScoringTests(DocVaultFactory factory) : base(factory)
    {
    }

    protected override async Task SeedTestDataAsync()
    {
        // Seed documents with shared term in different locations for scoring comparison
        var documents = new[]
        {
            ($"{SearchTestConstants.SHARED_TERM} Overview — {SearchTestConstants.SHARED_TERM}", 
             $"An introduction to plant biology. {SearchTestConstants.SHARED_TERM} ecosystems."),
            ("Practical Science Handbook", 
             $"This handbook covers topics and {SearchTestConstants.SHARED_TERM} in detail.")
        };

        var documentIds = await DataSeeder.SeedMultipleDocumentsAsync(documents);
        _titleAndTextDocumentId = documentIds[0];
        _textOnlyDocumentId = documentIds[1];
    }

    [Fact]
    public async Task Search_TitleHit_ScoresHigherThanTextOnlyHit()
    {
        // Act
        var page = await SearchHelpers.ExecuteSearchAsync(SearchTestConstants.SHARED_TERM, size: 10);

        // Assert
        var titleAndTextScore = page.Items.Single(i => i.Id == _titleAndTextDocumentId).Score;
        var textOnlyScore = page.Items.Single(i => i.Id == _textOnlyDocumentId).Score;

        Assert.True(titleAndTextScore > textOnlyScore,
            $"Expected title+text score ({titleAndTextScore}) > text-only score ({textOnlyScore})");
    }

    [Fact]
    public async Task Search_AllMatchingScores_AreInValidRange()
    {
        // Act
        var page = await SearchHelpers.ExecuteSearchAsync(SearchTestConstants.SHARED_TERM, size: 10);

        // Assert
        SearchAssertions.AssertScoreInRange(page, minScore: 0.0, maxScore: 1.0);
    }

    [Fact]
    public async Task Search_ResultsAreOrderedByScoreDescending()
    {
        // Act
        var page = await SearchHelpers.ExecuteSearchAsync(SearchTestConstants.SHARED_TERM, size: 10);

        // Assert
        Assert.True(page.Items.Count >= 2, "Need at least 2 results to test ordering");
        
        for (int i = 0; i < page.Items.Count - 1; i++)
        {
            Assert.True(page.Items[i].Score >= page.Items[i + 1].Score,
                $"Results should be ordered by score descending. Item {i} score: {page.Items[i].Score}, Item {i + 1} score: {page.Items[i + 1].Score}");
        }
    }
}
