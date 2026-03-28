using Xunit;

namespace DocVault.IntegrationTests.Infrastructure;

/// <summary>
/// Defines a test collection that all integration tests should use.
/// This ensures proper sharing of the DocVaultFactory fixture and prevents
/// multiple instances from being created concurrently.
/// </summary>
[CollectionDefinition("DocVault Integration Tests")]
public class DocVaultTestCollection : ICollectionFixture<DocVaultFactory>
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
}

