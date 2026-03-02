using DocVault.Application.Abstractions.Embeddings;
using DocVault.Application.Abstractions.Messaging;
using DocVault.Application.Abstractions.Persistence;
using DocVault.Application.Abstractions.Storage;
using DocVault.Application.Abstractions.Text;
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

public static class DependencyInjection
{
  public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
  {
    var connectionString = configuration.GetConnectionString("Database");

    services.AddDbContext<DocVaultDbContext>(options =>
    {
      if (string.IsNullOrWhiteSpace(connectionString))
      {
        options.UseInMemoryDatabase("docvault");
      }
      else
      {
        options.UseNpgsql(connectionString);
      }
    });

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
