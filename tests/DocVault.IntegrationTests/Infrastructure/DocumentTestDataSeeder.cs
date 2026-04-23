using DocVault.Application.Abstractions.Persistence;
using DocVault.Domain.Documents;
using DocVault.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DocVault.IntegrationTests.Infrastructure;

/// <summary>
/// Concrete implementation of document test data seeder.
/// Follows Dependency Inversion Principle - depends on abstractions (IServiceProvider).
/// </summary>
public sealed class DocumentTestDataSeeder : IDocumentTestDataSeeder
{
    private readonly IServiceProvider _serviceProvider;

    public DocumentTestDataSeeder(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DocVaultDbContext>();

        // Clear any existing documents to ensure test isolation
        await CleanupAsync(cancellationToken);
    }

    public async Task CleanupAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DocVaultDbContext>();

        var allDocuments = await context.Documents.ToListAsync(cancellationToken);
        context.Documents.RemoveRange(allDocuments);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<Guid> SeedDocumentAsync(string title, string content, CancellationToken cancellationToken = default)
    {
        using var scope = _serviceProvider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IDocumentRepository>();
        var context    = scope.ServiceProvider.GetRequiredService<DocVaultDbContext>();

        var document = DocumentTestBuilder.New()
            .WithTitle(title)
            .WithContent(content)
            .Build();

        await repository.AddAsync(document, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        return document.Id.Value;
    }

    public async Task<IReadOnlyList<Guid>> SeedMultipleDocumentsAsync(
        IEnumerable<(string Title, string Content)> documents, 
        CancellationToken cancellationToken = default)
    {
        using var scope = _serviceProvider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IDocumentRepository>();
        var context    = scope.ServiceProvider.GetRequiredService<DocVaultDbContext>();

        var documentIds = new List<Guid>();

        foreach (var (title, content) in documents)
        {
            var document = DocumentTestBuilder.New()
                .WithTitle(title)
                .WithContent(content)
                .Build();

            await repository.AddAsync(document, cancellationToken);
            documentIds.Add(document.Id.Value);
        }

        await context.SaveChangesAsync(cancellationToken);
        return documentIds.AsReadOnly();
    }
}

