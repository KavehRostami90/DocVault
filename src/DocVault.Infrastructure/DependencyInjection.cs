using DocVault.Application.Abstractions.Embeddings;
using DocVault.Application.Abstractions.Messaging;
using DocVault.Application.Abstractions.Persistence;
using DocVault.Application.Abstractions.Storage;
using DocVault.Application.Abstractions.Text;
using DocVault.Application.Background.Queue;
using DocVault.Infrastructure.Embeddings;
using DocVault.Infrastructure.Messaging;
using DocVault.Infrastructure.Messaging.Handlers;
using DocVault.Infrastructure.Persistence;
using DocVault.Infrastructure.Persistence.Repositories;
using DocVault.Infrastructure.Storage;
using DocVault.Infrastructure.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DocVault.Infrastructure;

/// <summary>
/// Extension methods for registering all infrastructure services with the DI container.
/// </summary>
public static class DependencyInjection
{
  /// <summary>
  /// Registers EF Core, repositories, storage, text extractors, embedding provider,
  /// messaging, and the database initializer.
  /// When a <c>Database:ConnectionString</c> is configured, PostgreSQL is used and
  /// <see cref="DocVault.Infrastructure.Messaging.PostgresWorkQueue"/> replaces the
  /// in-process channel queue; otherwise an in-memory database is used.
  /// </summary>
  /// <param name="services">The service collection to configure.</param>
  /// <param name="configuration">The application configuration.</param>
  /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
  public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
  {
    var connectionString = configuration.GetConnectionString("Database");

    if (string.IsNullOrWhiteSpace(connectionString))
    {
      services.AddDbContext<DocVaultDbContext>(options => options.UseInMemoryDatabase("docvault"));
    }
    else
    {
      // AddDbContextFactory registers DbContextOptions<T> as Singleton, which allows the
      // factory itself to be Singleton (consumed by PostgresWorkQueue).
      // The explicit AddScoped below gives repositories the per-request DbContext they expect,
      // without re-registering options as Scoped (which would cause the Singleton → Scoped
      // lifetime violation caught by the DI validator).
      services.AddDbContextFactory<DocVaultDbContext>(options => options.UseNpgsql(connectionString));
      services.AddScoped(sp =>
        sp.GetRequiredService<IDbContextFactory<DocVaultDbContext>>().CreateDbContext());

      // Override the in-process ChannelWorkQueue registered by the Application layer.
      services.AddSingleton<IWorkQueue<IndexingWorkItem>, PostgresWorkQueue>();
    }

    services.AddScoped<IDocumentRepository, EfDocumentRepository>();
    services.AddScoped<ITagRepository, EfTagRepository>();
    services.AddScoped<IImportJobRepository, EfImportJobRepository>();

    services.AddSingleton<IFileStorage>(_ => new LocalFileStorage(Path.Combine(AppContext.BaseDirectory, "storage")));
    services.AddSingleton<ITextExtractor, PlainTextExtractor>();
    services.AddSingleton<IEmbeddingProvider, FakeEmbeddingProvider>();

    services.AddSingleton<IDomainEventDispatcher, InProcessDomainEventDispatcher>();
    services.AddSingleton<DocumentImportedHandler>();
    services.AddSingleton<SearchExecutedHandler>();

    services.AddHostedService<DatabaseInitializer>();

    return services;
  }
}
