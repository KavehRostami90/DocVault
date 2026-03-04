using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using DocVault.IntegrationTests.Infrastructure;
using Xunit;

namespace DocVault.IntegrationTests.Api.Search;

/// <summary>
/// Tests for search validation and error handling.
/// Follows Single Responsibility Principle - only tests validation behavior.
/// </summary>
public sealed class SearchValidationTests : BaseIntegrationTest
{
    public SearchValidationTests(DocVaultFactory factory) : base(factory)
    {
    }

    protected override async Task SeedTestDataAsync()
    {
        // No test data needed for validation tests
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Search_EmptyQuery_Returns400WithValidationError()
    {
        // Act
        var response = await SearchHelpers.ExecuteSearchRawAsync(new { query = "", page = 1, size = 10 });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var body = await ParseJsonAsync(response);
        var errors = body["errors"]?.AsObject();
        Assert.NotNull(errors);
        Assert.True(errors!.ContainsKey("Query"), $"Expected 'Query' validation error. Got: {errors}");
    }

    [Fact]
    public async Task Search_QueryExceedsMaxLength_Returns400()
    {
        // Arrange
        var longQuery = new string('a', ValidationTestConstants.MAX_QUERY_LENGTH + 1);

        // Act
        var response = await SearchHelpers.ExecuteSearchRawAsync(new { query = longQuery, page = 1, size = 10 });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Search_InvalidPage_Returns400()
    {
        // Act
        var response = await SearchHelpers.ExecuteSearchRawAsync(new { query = "test", page = 0, size = 10 });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Search_InvalidPageSize_Returns400()
    {
        // Act
        var response = await SearchHelpers.ExecuteSearchRawAsync(new { query = "test", page = 1, size = 0 });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Search_ExcessivePageSize_Returns400()
    {
        // Act
        var response = await SearchHelpers.ExecuteSearchRawAsync(new { query = "test", page = 1, size = ValidationTestConstants.MAX_PAGE_SIZE + 1 });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Search_MissingQueryParameter_Returns400()
    {
        // Act
        var response = await SearchHelpers.ExecuteSearchRawAsync(new { page = 1, size = 10 });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Search_ErrorResponse_ContainsProblemDetailsFields()
    {
        // Act
        var response = await SearchHelpers.ExecuteSearchRawAsync(new { query = "", page = 1, size = 10 });
        var body = await ParseJsonAsync(response);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("status", body.Select(kv => kv.Key));
        Assert.Contains("errors", body.Select(kv => kv.Key));
        Assert.Contains("traceId", body.Select(kv => kv.Key));
    }

    private static async Task<JsonObject> ParseJsonAsync(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<JsonObject>(content,
                 new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
               ?? throw new InvalidOperationException($"Could not parse body: {content}");
    }
}
