using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DocVault.IntegrationTests.Infrastructure;

/// <summary>
/// Base class for integration tests following SOLID principles.
/// Provides common functionality while allowing specific test classes to focus on their responsibilities.
/// </summary>
[Collection("DocVault Integration Tests")]
public abstract class BaseIntegrationTest : IAsyncLifetime
{
    protected readonly DocVaultFactory Factory;
    protected readonly HttpClient HttpClient;
    protected readonly IDocumentTestDataSeeder DataSeeder;
    protected readonly ISearchTestAssertions SearchAssertions;
    protected readonly SearchTestHelpers SearchHelpers;

    protected BaseIntegrationTest(DocVaultFactory factory)
    {
        Factory = factory ?? throw new ArgumentNullException(nameof(factory));
        HttpClient = factory.CreateClient();
        DataSeeder = new DocumentTestDataSeeder(factory.Services);
        SearchAssertions = new SearchTestAssertions();
        SearchHelpers = new SearchTestHelpers(HttpClient);
    }

    /// <summary>
    /// Template method - allows derived classes to provide specific initialization logic.
    /// Follows Open/Closed Principle - base class is closed for modification but open for extension.
    /// </summary>
    public virtual async Task InitializeAsync()
    {
        await DataSeeder.CleanupAsync();
        await SeedTestDataAsync();
    }

    /// <summary>
    /// Template method for cleanup - can be overridden by derived classes.
    /// </summary>
    public virtual async Task DisposeAsync()
    {
        await DataSeeder.CleanupAsync();
    }

    /// <summary>
    /// Abstract method that forces derived classes to implement their specific data seeding logic.
    /// Follows Template Method pattern.
    /// </summary>
    protected abstract Task SeedTestDataAsync();

    /// <summary>
    /// Helper method to create scoped services.
    /// Follows Dependency Inversion Principle.
    /// </summary>
    protected T GetService<T>() where T : notnull
    {
        using var scope = Factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<T>();
    }
}

