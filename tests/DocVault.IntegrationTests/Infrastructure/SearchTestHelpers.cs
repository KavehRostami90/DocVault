using System.Net.Http.Json;
using System.Text.Json;

namespace DocVault.IntegrationTests.Infrastructure;

/// <summary>
/// Helper class for search-related operations in tests.
/// Follows Single Responsibility Principle - handles only search operations.
/// </summary>
public sealed class SearchTestHelpers
{
    private readonly HttpClient _httpClient;

    public SearchTestHelpers(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public async Task<SearchPage> ExecuteSearchAsync(string query, int page = 1, int size = 10, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("/search/documents",
            new { query, page, size }, cancellationToken);

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<SearchPage>(json,
                 new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
               ?? throw new InvalidOperationException($"Could not deserialize response: {json}");
    }

    public async Task<HttpResponseMessage> ExecuteSearchRawAsync(object request, CancellationToken cancellationToken = default)
    {
        return await _httpClient.PostAsJsonAsync("/search/documents", request, cancellationToken);
    }
}

/// <summary>
/// DTOs for search responses - kept local to avoid dependencies on API contracts.
/// </summary>
public sealed class SearchPage
{
    public List<SearchItem> Items { get; init; } = [];
    public int Page { get; init; }
    public int Size { get; init; }
    public long TotalCount { get; init; }
}

public sealed class SearchItem
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Snippet { get; init; } = string.Empty;
    public double Score { get; init; }
}
