using System.Net;

namespace DocVault.IntegrationTests.Infrastructure;

/// <summary>
/// Helper class for document upload operations in tests.
/// Follows Single Responsibility Principle - handles only upload operations.
/// </summary>
public sealed class DocumentUploadTestHelpers
{
    private readonly HttpClient _httpClient;

    public DocumentUploadTestHelpers(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public async Task<HttpResponseMessage> UploadDocumentAsync(
        (byte[] Bytes, string FileName, string ContentType) file,
        string title,
        string[]? tags = null,
        CancellationToken cancellationToken = default)
    {
        using var form = BuildMultipartForm(file, title, tags);
        return await _httpClient.PostAsync("/api/v1/documents", form, cancellationToken);
    }

    public async Task<HttpResponseMessage> UploadDocumentWithMissingFieldAsync(
        string? title = null,
        (byte[] Bytes, string FileName, string ContentType)? file = null,
        CancellationToken cancellationToken = default)
    {
        using var form = new MultipartFormDataContent();

        if (title != null)
        {
            form.Add(new StringContent(title), "title");
        }

        if (file.HasValue)
        {
            var fileContent = new ByteArrayContent(file.Value.Bytes);
            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(file.Value.ContentType);
            form.Add(fileContent, "file", file.Value.FileName);
        }

        return await _httpClient.PostAsync("/api/v1/documents", form, cancellationToken);
    }

    private static MultipartFormDataContent BuildMultipartForm(
        (byte[] Bytes, string FileName, string ContentType) file,
        string title,
        string[]? tags = null)
    {
        var form = new MultipartFormDataContent();

        var fileContent = new ByteArrayContent(file.Bytes);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(file.ContentType);
        form.Add(fileContent, "file", file.FileName);
        form.Add(new StringContent(title), "title");

        foreach (var tag in tags ?? [])
        {
            form.Add(new StringContent(tag), "tags");
        }

        return form;
    }
}

