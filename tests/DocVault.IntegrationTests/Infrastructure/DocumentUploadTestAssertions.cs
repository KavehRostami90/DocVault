using System.Net;
using System.Text.Json.Nodes;
using Xunit;

namespace DocVault.IntegrationTests.Infrastructure;

/// <summary>
/// Assertions for document upload test scenarios.
/// Follows Single Responsibility Principle and Interface Segregation Principle.
/// </summary>
public interface IDocumentUploadTestAssertions
{
    void AssertSuccessfulUpload(HttpResponseMessage response, JsonObject? responseBody = null);
    void AssertValidationError(HttpResponseMessage response, string expectedFieldName);
    void AssertBadRequest(HttpResponseMessage response);
    void AssertProblemDetailsStructure(JsonObject body);
}

public sealed class DocumentUploadTestAssertions : IDocumentUploadTestAssertions
{
    public void AssertSuccessfulUpload(HttpResponseMessage response, JsonObject? responseBody = null)
    {
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        if (responseBody != null)
        {
            Assert.True(responseBody.ContainsKey("id"));
            Assert.True(Guid.TryParse(responseBody["id"]?.GetValue<string>(), out _));
        }
    }

    public void AssertValidationError(HttpResponseMessage response, string expectedFieldName)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        // Additional validation logic can be added here
    }

    public void AssertBadRequest(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    public void AssertProblemDetailsStructure(JsonObject body)
    {
        Assert.Contains("status", body.Select(kv => kv.Key));
        Assert.Contains("errors", body.Select(kv => kv.Key));
        Assert.Contains("traceId", body.Select(kv => kv.Key));
    }
}

