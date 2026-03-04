using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using DocVault.IntegrationTests.Infrastructure;
using Xunit;

namespace DocVault.IntegrationTests.Api.Documents;

/// <summary>
/// Tests for successful document upload scenarios.
/// Follows Single Responsibility Principle - only tests happy path uploads.
/// </summary>
public sealed class DocumentUploadSuccessTests : BaseIntegrationTest
{
    private readonly DocumentUploadTestHelpers _uploadHelpers;
    private readonly IDocumentUploadTestAssertions _uploadAssertions;

    public DocumentUploadSuccessTests(DocVaultFactory factory) : base(factory)
    {
        _uploadHelpers = new DocumentUploadTestHelpers(HttpClient);
        _uploadAssertions = new DocumentUploadTestAssertions();
    }

    protected override async Task SeedTestDataAsync()
    {
        // No test data needed for upload tests
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Post_ValidPdf_Returns201WithId()
    {
        // Act
        var response = await _uploadHelpers.UploadDocumentAsync(
            DocumentUploadTestData.ValidFiles.Pdf,
            DocumentUploadTestData.ValidTitles.QuarterlyReport,
            DocumentUploadTestData.ValidTags.Finance);

        // Assert
        var body = await ParseJsonAsync(response);
        _uploadAssertions.AssertSuccessfulUpload(response, body);
    }

    [Fact]
    public async Task Post_ValidTxt_Returns201()
    {
        // Act
        var response = await _uploadHelpers.UploadDocumentAsync(
            DocumentUploadTestData.ValidFiles.TextFile,
            DocumentUploadTestData.ValidTitles.MeetingNotes);

        // Assert
        _uploadAssertions.AssertSuccessfulUpload(response);
    }

    [Fact]
    public async Task Post_NoTags_Returns201()
    {
        // Act
        var response = await _uploadHelpers.UploadDocumentAsync(
            DocumentUploadTestData.ValidFiles.TextFile,
            DocumentUploadTestData.ValidTitles.PlainDocument,
            DocumentUploadTestData.ValidTags.Empty);

        // Assert
        _uploadAssertions.AssertSuccessfulUpload(response);
    }

    [Fact]
    public async Task Post_WithTags_Returns201()
    {
        // Act
        var response = await _uploadHelpers.UploadDocumentAsync(
            DocumentUploadTestData.ValidFiles.TextFile,
            DocumentUploadTestData.ValidTitles.MeetingNotes,
            DocumentUploadTestData.ValidTags.Meeting);

        // Assert
        _uploadAssertions.AssertSuccessfulUpload(response);
    }

    private static async Task<JsonObject> ParseJsonAsync(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<JsonObject>(content,
                 new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
               ?? throw new InvalidOperationException($"Could not parse body: {content}");
    }
}
