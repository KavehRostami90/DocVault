using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using DocVault.Application.Abstractions.Persistence;
using DocVault.Domain.Documents;
using DocVault.Domain.Documents.ValueObjects;
using DocVault.Infrastructure.Persistence;
using DocVault.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DocVault.IntegrationTests.Api;

/// <summary>
/// Integration tests for POST /search/documents.
/// Documents are seeded directly via IDocumentRepository so tests are
/// deterministic and do not rely on the background indexing worker.
/// </summary>
public sealed class SearchTests : BaseIntegrationTest
{
  // Unique sentinel terms so seeded docs don't collide with anything uploaded
  // by other test classes sharing infrastructure.
  private const string TITLE_ONLY_TERM = "Photosynthesis8472";
  private const string TEXT_ONLY_TERM  = "chlorophyll9318";
  private const string SHARED_TERM     = "biology5501";
  private const string NO_MATCH_TERM   = "zxkqwerty00000";

  // Seeded document ids — resolved in SeedTestDataAsync so assertions can filter by id.
  private Guid _aiDocId;
  private Guid _mlDocId;
  private Guid _dockerDocId;

  public SearchTests(DocVaultFactory factory) : base(factory)
  {
  }

  // ---------------------------------------------------------------------------
  // Seed
  // ---------------------------------------------------------------------------

  protected override async Task SeedTestDataAsync()
  {
    using var scope = Factory.Services.CreateScope();
    var repo = scope.ServiceProvider.GetRequiredService<IDocumentRepository>();
    var context = scope.ServiceProvider.GetRequiredService<DocVaultDbContext>();

    // Clear any documents from previous tests in this class fixture
    var allDocs = await context.Documents.ToListAsync();
    context.Documents.RemoveRange(allDocs);
    await context.SaveChangesAsync();

    // Doc 1 — TITLE_ONLY_TERM in title; SHARED_TERM in both title and text
    var doc1 = MakeDocument(
      title: $"{TITLE_ONLY_TERM} Overview — {SHARED_TERM}",
      text:  $"An introduction to plant biology. {SHARED_TERM} ecosystems.");
    await repo.AddAsync(doc1);
    _aiDocId = doc1.Id.Value;

    // Doc 2 — TEXT_ONLY_TERM only in text; SHARED_TERM in text
    var doc2 = MakeDocument(
      title: "Practical Science Handbook",
      text:  $"This handbook covers {TEXT_ONLY_TERM} and {SHARED_TERM} in detail.");
    await repo.AddAsync(doc2);
    _mlDocId = doc2.Id.Value;

    // Doc 3 — completely unrelated to the sentinel terms
    var doc3 = MakeDocument(
      title: "Docker Container Orchestration",
      text:  "Kubernetes and docker containers for microservices deployment.");
    await repo.AddAsync(doc3);
    _dockerDocId = doc3.Id.Value;
  }

  // ---------------------------------------------------------------------------
  // Happy path — result shape
  // ---------------------------------------------------------------------------

  [Fact]
  public async Task Search_MatchingTitleTerm_ReturnsDocumentWithCorrectFields()
  {
    var page = await PostSearchAsync(TITLE_ONLY_TERM);

    Assert.Equal(1, page.TotalCount);
    Assert.Single(page.Items);

    var item = page.Items[0];
    Assert.Equal(_aiDocId, item.Id);
    Assert.False(string.IsNullOrWhiteSpace(item.Title));
    Assert.False(string.IsNullOrWhiteSpace(item.Snippet));
    Assert.True(item.Score > 0, $"Expected score > 0, got {item.Score}");
  }

  [Fact]
  public async Task Search_MatchingTextOnlyTerm_ReturnsDocument()
  {
    var page = await PostSearchAsync(TEXT_ONLY_TERM);

    Assert.Equal(1, page.TotalCount);
    Assert.Equal(_mlDocId, page.Items[0].Id);
  }

  [Fact]
  public async Task Search_NoMatchingDocuments_ReturnsEmptyPage()
  {
    var page = await PostSearchAsync(NO_MATCH_TERM);

    Assert.Equal(0, page.TotalCount);
    Assert.Empty(page.Items);
  }

  // ---------------------------------------------------------------------------
  // TotalCount correctness
  // ---------------------------------------------------------------------------

  [Fact]
  public async Task Search_TotalCount_ReflectsCorpusNotPageSize()
  {
    // SHARED_TERM appears in both doc1 title+text and doc2 text → 2 matches
    var page = await PostSearchAsync(SHARED_TERM, page: 1, size: 1);

    Assert.Equal(2, page.TotalCount);
    Assert.Single(page.Items);
  }

  [Fact]
  public async Task Search_SecondPage_ReturnsDistinctItem()
  {
    var page1 = await PostSearchAsync(SHARED_TERM, page: 1, size: 1);
    var page2 = await PostSearchAsync(SHARED_TERM, page: 2, size: 1);

    Assert.Single(page1.Items);
    Assert.Single(page2.Items);
    Assert.NotEqual(page1.Items[0].Id, page2.Items[0].Id);
  }

  [Fact]
  public async Task Search_PageBeyondResults_ReturnsEmptyItemsButCorrectTotal()
  {
    var page = await PostSearchAsync(SHARED_TERM, page: 999, size: 10);

    Assert.Equal(2, page.TotalCount);
    Assert.Empty(page.Items);
  }

  // ---------------------------------------------------------------------------
  // Scoring
  // ---------------------------------------------------------------------------

  [Fact]
  public async Task Search_TitleHit_ScoresHigherThanTextOnlyHit()
  {
    // SHARED_TERM appears in doc1 title AND text → higher score than doc2 (text only)
    var page = await PostSearchAsync(SHARED_TERM, size: 10);

    var doc1Score = page.Items.Single(i => i.Id == _aiDocId).Score;
    var doc2Score = page.Items.Single(i => i.Id == _mlDocId).Score;

    Assert.True(doc1Score > doc2Score,
      $"Expected title+text score ({doc1Score}) > text-only score ({doc2Score})");
  }

  [Fact]
  public async Task Search_AllMatchingScores_AreInZeroToOneRange()
  {
    var page = await PostSearchAsync(SHARED_TERM, size: 10);

    Assert.All(page.Items, item =>
    {
      Assert.True(item.Score >= 0 && item.Score <= 1.0,
        $"Score {item.Score} for '{item.Title}' is outside [0, 1]");
    });
  }

  // ---------------------------------------------------------------------------
  // Snippet length
  // ---------------------------------------------------------------------------

  [Fact]
  public async Task Search_Snippet_IsAtMost120Characters()
  {
    var page = await PostSearchAsync(TITLE_ONLY_TERM);

    Assert.Single(page.Items);
    Assert.True(page.Items[0].Snippet.Length <= 120,
      $"Snippet too long: {page.Items[0].Snippet.Length} chars");
  }

  // ---------------------------------------------------------------------------
  // Validation errors
  // ---------------------------------------------------------------------------

  [Fact]
  public async Task Search_EmptyQuery_Returns400WithValidationError()
  {
    var response = await HttpClient.PostAsJsonAsync("/api/v1/search/documents", new { query = "", page = 1, size = 10 });

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

    var body = await ParseAsync(response);
    var errors = body["errors"]?.AsObject();
    Assert.NotNull(errors);
    Assert.True(errors!.ContainsKey("Query"), $"Expected 'Query' validation error. Got: {errors}");
  }

  [Fact]
  public async Task Search_QueryExceedsMaxLength_Returns400()
  {
    var longQuery = new string('a', 513);
    var response = await HttpClient.PostAsJsonAsync("/api/v1/search/documents", new { query = longQuery, page = 1, size = 10 });

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
  }

  [Fact]
  public async Task Search_InvalidPage_Returns400()
  {
    var response = await HttpClient.PostAsJsonAsync("/api/v1/search/documents", new { query = "test", page = 0, size = 10 });

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
  }

  // ---------------------------------------------------------------------------
  // Helpers
  // ---------------------------------------------------------------------------

  private async Task<SearchPage> PostSearchAsync(string query, int page = 1, int size = 10)
  {
    var response = await HttpClient.PostAsJsonAsync("/api/v1/search/documents",
      new { query, page, size });

    response.EnsureSuccessStatusCode();

    var json = await response.Content.ReadAsStringAsync();
    return JsonSerializer.Deserialize<SearchPage>(json,
             new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
           ?? throw new InvalidOperationException($"Could not deserialise response: {json}");
  }

  private static async Task<JsonObject> ParseAsync(HttpResponseMessage response)
  {
    var content = await response.Content.ReadAsStringAsync();
    return JsonSerializer.Deserialize<JsonObject>(content,
             new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
           ?? throw new InvalidOperationException($"Could not parse body: {content}");
  }

  private static Document MakeDocument(string title, string text)
  {
    var id   = DocumentId.New();
    var hash = new FileHash(Guid.NewGuid().ToString("N"));
    var doc  = new Document(id, title, "seed.txt", "text/plain", text.Length, hash, TestAuthHandler.TestUserId);
    doc.AttachText(text);
    doc.MarkIndexed();
    return doc;
  }

  // Thin local DTOs — avoids a project reference on Api contracts just for tests.
  private sealed class SearchPage
  {
    public List<SearchItem> Items { get; init; } = [];
    public int Page    { get; init; }
    public int Size    { get; init; }
    public long TotalCount { get; init; }
  }

  private sealed class SearchItem
  {
    public Guid   Id      { get; init; }
    public string Title   { get; init; } = string.Empty;
    public string Snippet { get; init; } = string.Empty;
    public double Score   { get; init; }
  }
}

