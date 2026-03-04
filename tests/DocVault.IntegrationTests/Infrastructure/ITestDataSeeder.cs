namespace DocVault.IntegrationTests.Infrastructure;

/// <summary>
/// Interface for seeding test data in integration tests.
/// Follows ISP - clients depend only on methods they need.
/// </summary>
public interface ITestDataSeeder
{
    Task SeedAsync(CancellationToken cancellationToken = default);
    Task CleanupAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Specific interface for document-related test data seeding.
/// </summary>
public interface IDocumentTestDataSeeder : ITestDataSeeder
{
    Task<Guid> SeedDocumentAsync(string title, string content, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Guid>> SeedMultipleDocumentsAsync(IEnumerable<(string Title, string Content)> documents, CancellationToken cancellationToken = default);
}
