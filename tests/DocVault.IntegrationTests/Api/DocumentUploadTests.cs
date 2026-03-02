using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace DocVault.IntegrationTests.Api;

public sealed class DocumentUploadTests : IClassFixture<DocVaultFactory>
{
  private readonly HttpClient _client;

  private static readonly byte[] PDF_BYTES = "%PDF-1.4 test"u8.ToArray();
  private static readonly byte[] TXT_BYTES = "Hello, integration test."u8.ToArray();

  public DocumentUploadTests(DocVaultFactory factory)
  {
    _client = factory.CreateClient();
  }

  // ---------------------------------------------------------------------------
  // Happy path
  // ---------------------------------------------------------------------------

  [Fact]
  public async Task Post_ValidPdf_Returns201WithId()
  {
    var response = await _client.PostAsync("/documents", BuildForm(
      file: (PDF_BYTES, "report.pdf", "application/pdf"),
      title: "Quarterly Report",
      tags: ["finance", "q4"]));

    Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    Assert.NotNull(response.Headers.Location);

    var body = await ParseJsonAsync(response);
    Assert.True(body.ContainsKey("id"));
    Assert.True(Guid.TryParse(body["id"]?.GetValue<string>(), out _));
  }

  [Fact]
  public async Task Post_ValidTxt_Returns201()
  {
    var response = await _client.PostAsync("/documents", BuildForm(
      file: (TXT_BYTES, "notes.txt", "text/plain"),
      title: "Meeting notes"));

    Assert.Equal(HttpStatusCode.Created, response.StatusCode);
  }

  [Fact]
  public async Task Post_NoTags_Returns201()
  {
    var response = await _client.PostAsync("/documents", BuildForm(
      file: (TXT_BYTES, "plain.txt", "text/plain"),
      title: "No tags doc"));

    Assert.Equal(HttpStatusCode.Created, response.StatusCode);
  }

  // ---------------------------------------------------------------------------
  // Validation — missing / bad fields
  // ---------------------------------------------------------------------------

  [Fact]
  public async Task Post_MissingFile_Returns400WithError()
  {
    using var form = new MultipartFormDataContent();
    form.Add(new StringContent("Some title"), "title");

    var response = await _client.PostAsync("/documents", form);

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    var body = await ParseJsonAsync(response);
    AssertValidationError(body, "File");
  }

  [Fact]
  public async Task Post_EmptyTitle_Returns400WithError()
  {
    var response = await _client.PostAsync("/documents", BuildForm(
      file: (TXT_BYTES, "file.txt", "text/plain"),
      title: ""));

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    var body = await ParseJsonAsync(response);
    AssertValidationError(body, "Title");
  }

  [Fact]
  public async Task Post_UnsupportedContentType_Returns400WithError()
  {
    var response = await _client.PostAsync("/documents", BuildForm(
      file: ([0xFF, 0xD8], "photo.jpg", "image/jpeg"),
      title: "A photo"));

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    var body = await ParseJsonAsync(response);

    // error may be keyed on File or File.ContentType depending on property path
    var errors = body["errors"]?.AsObject();
    Assert.NotNull(errors);
    Assert.True(errors!.Any(), "Expected at least one validation error");
  }

  [Fact]
  public async Task Post_JsonBody_Returns400MustBeMultipart()
  {
    var response = await _client.PostAsJsonAsync("/documents", new { title = "test", fileName = "test.pdf" });

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
  }

  // ---------------------------------------------------------------------------
  // Response shape
  // ---------------------------------------------------------------------------

  [Fact]
  public async Task Post_ValidRequest_ResponseContainsProblemDetailsFieldsOnError()
  {
    using var form = new MultipartFormDataContent();
    form.Add(new StringContent(""), "title");

    var response = await _client.PostAsync("/documents", form);
    var body = await ParseJsonAsync(response);

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    Assert.Contains("status",  body.Select(kv => kv.Key));
    Assert.Contains("errors",  body.Select(kv => kv.Key));
    Assert.Contains("traceId", body.Select(kv => kv.Key));
  }

  // ---------------------------------------------------------------------------
  // Helpers
  // ---------------------------------------------------------------------------

  private static MultipartFormDataContent BuildForm(
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
      form.Add(new StringContent(tag), "tags");

    return form;
  }

  private static async Task<JsonObject> ParseJsonAsync(HttpResponseMessage response)
  {
    var content = await response.Content.ReadAsStringAsync();
    return JsonSerializer.Deserialize<JsonObject>(content,
             new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
           ?? throw new InvalidOperationException($"Could not parse response body: {content}");
  }

  private static void AssertValidationError(JsonObject body, string propertyName)
  {
    var errors = body["errors"]?.AsObject();
    Assert.NotNull(errors);
    Assert.True(
      errors!.ContainsKey(propertyName),
      $"Expected validation error for '{propertyName}'. Got: {errors}");
  }
}
