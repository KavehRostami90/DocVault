using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using DocVault.IntegrationTests.Infrastructure;
using Xunit;

namespace DocVault.IntegrationTests.Api.Documents;

/// <summary>
/// Tests for document upload validation scenarios.
/// Follows Single Responsibility Principle - only tests validation and error cases.
/// </summary>
public sealed class DocumentUploadValidationTests : BaseIntegrationTest
{
    private readonly DocumentUploadTestHelpers _uploadHelpers;
    private readonly IDocumentUploadTestAssertions _uploadAssertions;

    public DocumentUploadValidationTests(DocVaultFactory factory) : base(factory)
    {
        _uploadHelpers = new DocumentUploadTestHelpers(HttpClient);
        _uploadAssertions = new DocumentUploadTestAssertions();
    }

    protected override async Task SeedTestDataAsync()
    {
        // No test data needed for validation tests
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Post_MissingFile_Returns400WithError()
    {
        // Act
        var response = await _uploadHelpers.UploadDocumentWithMissingFieldAsync(
            title: DocumentUploadTestData.ValidTitles.QuarterlyReport);

        // Assert
        _uploadAssertions.AssertValidationError(response, "File");
        var body = await ParseJsonAsync(response);
        AssertValidationErrorField(body, "File");
    }

    [Fact]
    public async Task Post_EmptyTitle_Returns400WithError()
    {
        // Act
        var response = await _uploadHelpers.UploadDocumentAsync(
            DocumentUploadTestData.ValidFiles.TextFile,
            DocumentUploadTestData.InvalidTitles.Empty);

        // Assert
        _uploadAssertions.AssertValidationError(response, "Title");
        var body = await ParseJsonAsync(response);
        AssertValidationErrorField(body, "Title");
    }

    [Fact]
    public async Task Post_MissingTitle_Returns400WithError()
    {
        // Act
        var response = await _uploadHelpers.UploadDocumentWithMissingFieldAsync(
            file: DocumentUploadTestData.ValidFiles.TextFile);

        // Assert
        _uploadAssertions.AssertValidationError(response, "Title");
    }

    [Fact]
    public async Task Post_UnsupportedContentType_Returns400WithError()
    {
        // Act
        var response = await _uploadHelpers.UploadDocumentAsync(
            DocumentUploadTestData.InvalidFiles.UnsupportedImage,
            DocumentUploadTestData.ValidTitles.QuarterlyReport);

        // Assert
        _uploadAssertions.AssertBadRequest(response);
        var body = await ParseJsonAsync(response);

        // Error may be keyed on File or File.ContentType depending on property path
        var errors = body["errors"]?.AsObject();
        Assert.NotNull(errors);
        Assert.True(errors!.Any(), "Expected at least one validation error");
    }

    [Fact]
    public async Task Post_JsonBody_Returns400MustBeMultipart()
    {
        // Act
        var response = await HttpClient.PostAsJsonAsync("/api/v1/documents", 
            new { title = "test", fileName = "test.pdf" });

        // Assert
        _uploadAssertions.AssertBadRequest(response);
    }

    [Fact]
    public async Task Post_ValidationError_ResponseContainsProblemDetailsFields()
    {
        // Act
        var response = await _uploadHelpers.UploadDocumentWithMissingFieldAsync(
            title: DocumentUploadTestData.InvalidTitles.Empty);
        var body = await ParseJsonAsync(response);

        // Assert
        _uploadAssertions.AssertBadRequest(response);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        _uploadAssertions.AssertProblemDetailsStructure(body);
    }

    private static async Task<JsonObject> ParseJsonAsync(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<JsonObject>(content,
                 new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
               ?? throw new InvalidOperationException($"Could not parse body: {content}");
    }

    private static void AssertValidationErrorField(JsonObject body, string expectedField)
    {
        var errors = body["errors"]?.AsObject();
        Assert.NotNull(errors);
        Assert.True(errors!.ContainsKey(expectedField), 
            $"Expected '{expectedField}' validation error. Got: {string.Join(", ", errors.Select(x => x.Key))}");
    }
}

