using DocVault.IntegrationTests.Infrastructure;
using Xunit;

namespace DocVault.IntegrationTests.Api.Search;

/// <summary>
/// Tests for search response format and structure.
/// Follows Single Responsibility Principle - only tests response formatting.
/// </summary>
public sealed class SearchResponseTests : BaseIntegrationTest
{
    private Guid _testDocumentId;

    public SearchResponseTests(DocVaultFactory factory) : base(factory)
    {
    }

    protected override async Task SeedTestDataAsync()
    {
        _testDocumentId = await DataSeeder.SeedDocumentAsync(
            $"Test Document {SearchTestConstants.TITLE_ONLY_TERM}",
            $"This is a test document with some content that includes {SearchTestConstants.TITLE_ONLY_TERM} for testing purposes."
        );
    }

    [Fact]
    public async Task Search_ResponseStructure_ContainsRequiredFields()
    {
        // Act
        var page = await SearchHelpers.ExecuteSearchAsync(SearchTestConstants.TITLE_ONLY_TERM);

        // Assert
        Assert.NotNull(page);
        Assert.NotNull(page.Items);
        Assert.True(page.Page > 0);
        Assert.True(page.Size > 0);
        Assert.True(page.TotalCount >= 0);
    }

    [Fact]
    public async Task Search_SearchItem_ContainsRequiredFields()
    {
        // Act
        var page = await SearchHelpers.ExecuteSearchAsync(SearchTestConstants.TITLE_ONLY_TERM);

        // Assert
        Assert.NotEmpty(page.Items);
        var item = page.Items[0];
        
        Assert.NotEqual(Guid.Empty, item.Id);
        Assert.False(string.IsNullOrWhiteSpace(item.Title));
        Assert.False(string.IsNullOrWhiteSpace(item.Snippet));
        Assert.True(item.Score >= 0 && item.Score <= 1.0);
    }

    [Fact]
    public async Task Search_Snippet_IsAtMostReasonableLength()
    {
        // Act
        var page = await SearchHelpers.ExecuteSearchAsync(SearchTestConstants.TITLE_ONLY_TERM);

        // Assert
        Assert.Single(page.Items);
        Assert.True(page.Items[0].Snippet.Length <= 120,
            $"Snippet too long: {page.Items[0].Snippet.Length} chars");
    }

    [Fact]
    public async Task Search_EmptyResult_HasCorrectStructure()
    {
        // Act
        var page = await SearchHelpers.ExecuteSearchAsync(SearchTestConstants.NO_MATCH_TERM);

        // Assert
        Assert.NotNull(page);
        Assert.Empty(page.Items);
        Assert.Equal(0, page.TotalCount);
        Assert.True(page.Page > 0);
        Assert.True(page.Size > 0);
    }
}
