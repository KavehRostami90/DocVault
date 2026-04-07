using DocVault.Application.Abstractions.Auth;
using DocVault.Application.Abstractions.Embeddings;
using DocVault.Application.Abstractions.Messaging;
using DocVault.Application.Abstractions.Persistence;
using DocVault.Application.Abstractions.Storage;
using DocVault.Application.Abstractions.Text;
using DocVault.Application.Background.Queue;
using DocVault.Infrastructure.Auth;
using DocVault.Infrastructure.Embeddings;
using DocVault.Infrastructure.Messaging;
using DocVault.Infrastructure.Messaging.Handlers;
using DocVault.Infrastructure.Persistence;
using DocVault.Infrastructure.Persistence.Repositories;
using DocVault.Infrastructure.Storage;
using DocVault.Infrastructure.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DocVault.Infrastructure;

public static class DependencyInjection
{
  public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
  {
    var connectionString = configuration.GetConnectionString("Database");

    if (string.IsNullOrWhiteSpace(connectionString))
    {
      services.AddDbContext<DocVaultDbContext>(options => options.UseInMemoryDatabase("docvault"));
    }
    else
    {
      services.AddDbContextFactory<DocVaultDbContext>(options => options.UseNpgsql(connectionString));
      services.AddScoped(sp =>
        sp.GetRequiredService<IDbContextFactory<DocVaultDbContext>>().CreateDbContext());

      services.AddSingleton<IWorkQueue<IndexingWorkItem>, PostgresWorkQueue>();
    }

    // ASP.NET Core Identity (roles + EF stores)
    services.AddIdentityCore<ApplicationUser>(options =>
    {
      options.Password.RequiredLength = 8;
      options.Password.RequireNonAlphanumeric = false;
      options.User.RequireUniqueEmail = true;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<DocVaultDbContext>()
    .AddDefaultTokenProviders();

    services.Configure<AuthSettings>(configuration.GetSection(AuthSettings.Section));
    services.AddScoped<ITokenService, JwtTokenService>();
    services.AddScoped<IdentitySeeder>();

    services.AddScoped<IDocumentRepository, EfDocumentRepository>();
    services.AddScoped<ITagRepository, EfTagRepository>();
    services.AddScoped<IImportJobRepository, EfImportJobRepository>();

    var azureBlobConnStr = configuration.GetConnectionString("AzureBlob");
    var azureBlobContainer = configuration["Storage:ContainerName"] ?? "docvault";
    if (!string.IsNullOrWhiteSpace(azureBlobConnStr))
    {
      services.AddSingleton<IFileStorage>(_ =>
        new AzureBlobFileStorage(azureBlobConnStr, azureBlobContainer));
    }
    else
    {
      services.AddSingleton<IFileStorage>(_ =>
        new LocalFileStorage(Path.Combine(AppContext.BaseDirectory, "storage")));
    }

    services.AddSingleton<ITextExtractor, PlainTextExtractor>();

    var openAiOptions = configuration.GetSection(OpenAiOptions.Section).Get<OpenAiOptions>() ?? new OpenAiOptions();
    if (openAiOptions.IsConfigured)
    {
      services.Configure<OpenAiOptions>(configuration.GetSection(OpenAiOptions.Section));
      services.AddHttpClient<IEmbeddingProvider, OpenAiEmbeddingProvider>();
    }
    else
    {
      services.AddSingleton<IEmbeddingProvider, FakeEmbeddingProvider>();
    }

    services.AddSingleton<IDomainEventDispatcher, InProcessDomainEventDispatcher>();
    services.AddSingleton<DocumentImportedHandler>();
    services.AddSingleton<SearchExecutedHandler>();

    services.AddHostedService<DatabaseInitializer>();

    return services;
  }
}
