using System.Net;
using System.Net.Http.Json;
using DocVault.Api.Contracts.Admin;
using DocVault.IntegrationTests.Infrastructure;
using Xunit;

namespace DocVault.IntegrationTests.Api;

/// <summary>
/// Integration tests for bulk admin document operations:
/// POST /admin/documents/bulk-delete and POST /admin/documents/bulk-reindex.
/// </summary>
[Collection("DocVault Integration Tests")]
public sealed class BulkAdminDocumentTests
{
    private readonly DocVaultFactory _factory;

    public BulkAdminDocumentTests(DocVaultFactory factory) => _factory = factory;

    // ─── Helpers ─────────────────────────────────────────────────────────────

    /// <summary>Seeds N indexed documents owned by the test admin and returns their IDs.</summary>
    private async Task<List<Guid>> SeedDocumentsAsync(int count)
    {
        var seeder = new DocumentTestDataSeeder(_factory.Services);
        var docs = new List<(string Title, string Content)>();
        for (int i = 0; i < count; i++)
            docs.Add(($"Bulk Test Doc {i}", $"content {i}"));

        var ids = await seeder.SeedMultipleDocumentsAsync(docs);
        return ids.ToList();
    }

    // ─── bulk-delete: admin access ────────────────────────────────────────────

    [Fact]
    public async Task BulkDelete_AllExistingIds_Returns200WithAllSucceeded()
    {
        var ids = await SeedDocumentsAsync(3);
        var client = _factory.CreateAdminClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/admin/documents/bulk-delete",
            new { Ids = ids });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<BulkOperationResponse>();
        Assert.NotNull(body);
        Assert.Equal(3, body.Succeeded);
        Assert.Equal(0, body.Failed);
    }

    [Fact]
    public async Task BulkDelete_MixedExistingAndMissingIds_CountsFailures()
    {
        var ids = await SeedDocumentsAsync(2);
        ids.Add(Guid.NewGuid()); // non-existent
        var client = _factory.CreateAdminClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/admin/documents/bulk-delete",
            new { Ids = ids });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<BulkOperationResponse>();
        Assert.NotNull(body);
        Assert.Equal(2, body.Succeeded);
        Assert.Equal(1, body.Failed);
    }

    [Fact]
    public async Task BulkDelete_EmptyIds_Returns200WithZeroCounts()
    {
        var client = _factory.CreateAdminClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/admin/documents/bulk-delete",
            new { Ids = Array.Empty<Guid>() });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<BulkOperationResponse>();
        Assert.NotNull(body);
        Assert.Equal(0, body.Succeeded);
        Assert.Equal(0, body.Failed);
    }

    // ─── bulk-delete: authorization ───────────────────────────────────────────

    [Fact]
    public async Task BulkDelete_WithUserRole_Returns403()
    {
        // Default factory client is signed in as a regular User, not Admin.
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/admin/documents/bulk-delete",
            new { Ids = new[] { Guid.NewGuid() } });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ─── bulk-reindex: admin access ───────────────────────────────────────────

    [Fact]
    public async Task BulkReindex_AllExistingIds_Returns200WithAllSucceeded()
    {
        var ids = await SeedDocumentsAsync(3);
        var client = _factory.CreateAdminClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/admin/documents/bulk-reindex",
            new { Ids = ids });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<BulkOperationResponse>();
        Assert.NotNull(body);
        Assert.Equal(3, body.Succeeded);
        Assert.Equal(0, body.Failed);
    }

    [Fact]
    public async Task BulkReindex_MixedExistingAndMissingIds_CountsFailures()
    {
        var ids = await SeedDocumentsAsync(2);
        ids.Add(Guid.NewGuid()); // non-existent
        var client = _factory.CreateAdminClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/admin/documents/bulk-reindex",
            new { Ids = ids });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<BulkOperationResponse>();
        Assert.NotNull(body);
        Assert.Equal(2, body.Succeeded);
        Assert.Equal(1, body.Failed);
    }

    [Fact]
    public async Task BulkReindex_EmptyIds_Returns200WithZeroCounts()
    {
        var client = _factory.CreateAdminClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/admin/documents/bulk-reindex",
            new { Ids = Array.Empty<Guid>() });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<BulkOperationResponse>();
        Assert.NotNull(body);
        Assert.Equal(0, body.Succeeded);
        Assert.Equal(0, body.Failed);
    }

    // ─── bulk-reindex: authorization ──────────────────────────────────────────

    [Fact]
    public async Task BulkReindex_WithUserRole_Returns403()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/admin/documents/bulk-reindex",
            new { Ids = new[] { Guid.NewGuid() } });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
