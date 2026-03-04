using DocVault.IntegrationTests.Infrastructure;
using Xunit;

namespace DocVault.IntegrationTests.Api.Search;

/// <summary>
/// Tests for basic document search functionality.
/// Follows Single Responsibility Principle - only tests core search behavior.
/// </summary>
public sealed class SearchDocumentTests : BaseIntegrationTest
{
    private Guid _titleOnlyDocumentId;
    private Guid _textOnlyDocumentId;
    private Guid _unrelatedDocumentId;

    public SearchDocumentTests(DocVaultFactory factory) : base(factory)
    {
    }

    protected override async Task SeedTestDataAsync()
    {
        // Seed documents with specific terms for testing
        var documents = new[]
        {
            ($"{SearchTestConstants.TITLE_ONLY_TERM} Overview — {SearchTestConstants.SHARED_TERM}", 
             $"An introduction to plant biology. {SearchTestConstants.SHARED_TERM} ecosystems."),
            ("Practical Science Handbook", 
             $"This handbook covers {SearchTestConstants.TEXT_ONLY_TERM} and {SearchTestConstants.SHARED_TERM} in detail."),
            ("Docker Container Orchestration", 
             "Kubernetes and docker containers for microservices deployment.")
        };

        var documentIds = await DataSeeder.SeedMultipleDocumentsAsync(documents);
        _titleOnlyDocumentId = documentIds[0];
        _textOnlyDocumentId = documentIds[1];
        _unrelatedDocumentId = documentIds[2];
    }

    [Fact]
    public async Task Search_MatchingTitleTerm_ReturnsDocumentWithCorrectFields()
    {
        // Act
        var page = await SearchHelpers.ExecuteSearchAsync(SearchTestConstants.TITLE_ONLY_TERM);

        // Assert
        SearchAssertions.AssertSearchResult(page, expectedCount: 1, _titleOnlyDocumentId);
        
        var item = page.Items[0];
        Assert.False(string.IsNullOrWhiteSpace(item.Title));
        Assert.False(string.IsNullOrWhiteSpace(item.Snippet));
        Assert.True(item.Score > 0, $"Expected score > 0, got {item.Score}");
    }

    [Fact]
    public async Task Search_MatchingTextOnlyTerm_ReturnsDocument()
    {
        // Act
        var page = await SearchHelpers.ExecuteSearchAsync(SearchTestConstants.TEXT_ONLY_TERM);

        // Assert
        SearchAssertions.AssertSearchResult(page, expectedCount: 1, _textOnlyDocumentId);
    }

    [Fact]
    public async Task Search_NoMatchingDocuments_ReturnsEmptyPage()
    {
        // Act
        var page = await SearchHelpers.ExecuteSearchAsync(SearchTestConstants.NO_MATCH_TERM);

        // Assert
        SearchAssertions.AssertEmptySearchResult(page);
    }

    [Fact]
    public async Task Search_SharedTerm_ReturnsMultipleDocuments()
    {
        // Act
        var page = await SearchHelpers.ExecuteSearchAsync(SearchTestConstants.SHARED_TERM);

        // Assert
        Assert.Equal(2, page.TotalCount);
        Assert.Equal(2, page.Items.Count);
        
        // Verify both documents are returned
        var returnedIds = page.Items.Select(i => i.Id).ToHashSet();
        Assert.Contains(_titleOnlyDocumentId, returnedIds);
        Assert.Contains(_textOnlyDocumentId, returnedIds);
    }
}
