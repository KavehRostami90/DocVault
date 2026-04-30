using DocVault.Application.Abstractions.Auth;
using DocVault.Infrastructure.Resilience;
using DocVault.Application.Abstractions.Email;
using DocVault.Application.Abstractions.Embeddings;
using DocVault.Application.Abstractions.Messaging;
using DocVault.Application.Abstractions.Persistence;
using DocVault.Application.Abstractions.Qa;
using DocVault.Application.Abstractions.Realtime;
using DocVault.Application.Abstractions.Storage;
using DocVault.Application.Abstractions.Text;
using DocVault.Application.Abstractions.Users;
using DocVault.Application.Background.Queue;
using Microsoft.Extensions.Options;
using DocVault.Domain.Events;
using DocVault.Infrastructure.Auth;
using DocVault.Infrastructure.Email;
using DocVault.Infrastructure.Embeddings;
using DocVault.Infrastructure.Messaging;
using DocVault.Infrastructure.Messaging.Handlers;
using DocVault.Infrastructure.Persistence;
using DocVault.Infrastructure.Persistence.Repositories;
using DocVault.Infrastructure.Qa;
using DocVault.Infrastructure.Queue;
using DocVault.Infrastructure.Realtime;
using DocVault.Infrastructure.Storage;
using DocVault.Infrastructure.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;

namespace DocVault.Infrastructure;

public static class DependencyInjection
{
  public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
  {
    var connectionString = configuration.GetConnectionString("Database");

    var dbOptions = configuration.GetSection(DatabaseOptions.Section).Get<DatabaseOptions>() ?? new DatabaseOptions();
    services.Configure<DatabaseOptions>(configuration.GetSection(DatabaseOptions.Section));

    if (string.IsNullOrWhiteSpace(connectionString))
    {
      services.AddDbContextFactory<DocVaultDbContext>(options =>
        options.UseInMemoryDatabase("docvault"));
      services.AddScoped(sp =>
        sp.GetRequiredService<IDbContextFactory<DocVaultDbContext>>().CreateDbContext());
      services.AddSingleton<IWorkQueue<IndexingWorkItem>>(_ => new ChannelWorkQueue<IndexingWorkItem>(5_000));
    }
    else
    {
      services.AddDbContextFactory<DocVaultDbContext>(options =>
        options.UseNpgsql(connectionString, o =>
        {
          o.UseVector();
          o.CommandTimeout(dbOptions.CommandTimeoutSeconds);
        }));
      services.AddScoped(sp =>
        sp.GetRequiredService<IDbContextFactory<DocVaultDbContext>>().CreateDbContext());
      services.AddSingleton<IWorkQueue<IndexingWorkItem>, PostgresWorkQueue>();
    }

    // ASP.NET Core Identity
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
    services.AddScoped<IUserService, IdentityUserService>();
    services.AddScoped<IEmailService, LogEmailService>();
    services.AddScoped<IdentitySeeder>();

    // Search strategies registered in priority order (first match wins).
    // Hybrid: vector + FTS terms → RRF fusion
    // Pgvector: vector only (no FTS terms) → pure semantic
    // Postgres: FTS only (no vector) → full-text keyword
    // InMemory: fallback for non-relational providers (tests)
    services.AddScoped<IDocumentSearchStrategy, HybridSearchStrategy>();
    services.AddScoped<IDocumentSearchStrategy, PgvectorSearchStrategy>();
    services.AddScoped<IDocumentSearchStrategy, PostgresSearchStrategy>();
    services.AddScoped<IDocumentSearchStrategy, InMemorySearchStrategy>();

    services.AddScoped<IDocumentRepository, EfDocumentRepository>();
    services.AddScoped<IDocumentChunkRepository, EfDocumentChunkRepository>();
    services.AddScoped<ITagRepository, EfTagRepository>();
    services.AddScoped<IImportJobRepository, EfImportJobRepository>();
    services.AddScoped<IUnitOfWork, EfUnitOfWork>();
    services.AddScoped<IUserQueryService, EfUserQueryService>();

    var azureBlobConnStr = configuration.GetConnectionString("AzureBlob");
    var azureBlobContainer = configuration["Storage:ContainerName"] ?? "docvault";
    if (!string.IsNullOrWhiteSpace(azureBlobConnStr))
    {
      services.AddSingleton<IFileStorage>(sp =>
      {
        sp.GetRequiredService<ILoggerFactory>()
          .CreateLogger("DocVault.Infrastructure")
          .LogInformation("File storage: AzureBlobFileStorage (container={Container})", azureBlobContainer);
        return new AzureBlobFileStorage(azureBlobConnStr, azureBlobContainer);
      });
    }
    else
    {
      services.AddSingleton<IFileStorage>(sp =>
      {
        sp.GetRequiredService<ILoggerFactory>()
          .CreateLogger("DocVault.Infrastructure")
          .LogInformation("File storage: LocalFileStorage (path={StoragePath})",
            Path.Combine(AppContext.BaseDirectory, "storage"));
        return new LocalFileStorage(Path.Combine(AppContext.BaseDirectory, "storage"));
      });
    }

    services.AddSingleton<ITextChunker, SimpleTextChunker>();

    services.Configure<OcrOptions>(configuration.GetSection(OcrOptions.Section));
    services.AddSingleton<IOcrEngine, CliTesseractOcrEngine>();
    services.AddSingleton<ImageOcrExtractor>();
    services.AddSingleton<PdfOcrExtractor>();
    services.AddSingleton<ITextExtractor, CompositeTextExtractor>();

    var openAiOptions = configuration.GetSection(OpenAiOptions.Section).Get<OpenAiOptions>() ?? new OpenAiOptions();
    services.Configure<QaOptions>(configuration.GetSection(QaOptions.Section));

    // Search result cache — short TTL to reduce repeated embedding + DB load for popular queries.
    services.Configure<SearchCacheOptions>(configuration.GetSection(SearchCacheOptions.Section));
    services.AddSingleton<ISearchResultCache, MemorySearchCache>();

    // Embedding cache — shared across both OpenAI and Fake providers.
    services.Configure<EmbeddingCacheOptions>(configuration.GetSection(EmbeddingCacheOptions.Section));
    services.AddSingleton<IEmbeddingCache, MemoryEmbeddingCache>();

    if (openAiOptions.IsConfigured)
    {
      services.Configure<OpenAiOptions>(configuration.GetSection(OpenAiOptions.Section));

      var resilience = configuration.GetSection(ResilienceOptions.Section).Get<ResilienceOptions>() ?? new ResilienceOptions();

      // Register the concrete HTTP client under its own type so the caching decorator
      // can resolve it without creating a circular IEmbeddingProvider dependency.
      services.AddHttpClient<OpenAiEmbeddingProvider>()
        .AddStandardResilienceHandler(o => ApplyResilience(o, resilience.Embedding));

      services.AddSingleton<IEmbeddingProvider>(sp => new CachingEmbeddingProvider(
        sp.GetRequiredService<OpenAiEmbeddingProvider>(),
        sp.GetRequiredService<IEmbeddingCache>(),
        openAiOptions.Model,
        sp.GetRequiredService<IOptions<EmbeddingCacheOptions>>(),
        sp.GetRequiredService<ILogger<CachingEmbeddingProvider>>()));

      services.AddHttpClient<IQuestionAnsweringService, OpenAiQuestionAnsweringService>()
        .AddStandardResilienceHandler(o => ApplyResilience(o, resilience.Qa));
    }
    else
    {
      services.AddSingleton<IEmbeddingProvider>(sp =>
      {
        sp.GetRequiredService<ILoggerFactory>()
          .CreateLogger("DocVault.Infrastructure")
          .LogWarning(
            "Embedding provider: FakeEmbeddingProvider — semantic search will produce meaningless results. " +
            "Configure OpenAI:ApiKey (or point OpenAI:BaseUrl at an Ollama instance) for real embeddings.");

        return new CachingEmbeddingProvider(
          new FakeEmbeddingProvider(),
          sp.GetRequiredService<IEmbeddingCache>(),
          "fake",
          sp.GetRequiredService<IOptions<EmbeddingCacheOptions>>(),
          sp.GetRequiredService<ILogger<CachingEmbeddingProvider>>());
      });
      services.AddSingleton<IQuestionAnsweringService, FallbackQuestionAnsweringService>();
    }

    services.AddSingleton<IDocumentStatusBroadcaster, DocumentStatusBroadcaster>();
    services.AddSingleton<IDomainEventDispatcher, InProcessDomainEventDispatcher>();
    services.AddSingleton<IEventHandler<DocumentImported>, DocumentImportedHandler>();
    services.AddSingleton<IEventHandler<DocumentIndexed>, DocumentIndexedEventHandler>();
    services.AddSingleton<IEventHandler<DocumentFailed>, DocumentFailedEventHandler>();
    services.AddSingleton<IEventHandler<SearchExecuted>, SearchExecutedHandler>();

    services.AddHostedService<DatabaseInitializer>();

    return services;
  }

  private static void ApplyResilience(HttpStandardResilienceOptions o, ClientResilienceOptions opts)
  {
    o.AttemptTimeout.Timeout      = TimeSpan.FromSeconds(opts.AttemptTimeoutSeconds);
    o.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(opts.TotalTimeoutSeconds);
    o.Retry.MaxRetryAttempts      = opts.MaxRetryAttempts;
    // Polly requires SamplingDuration > 2× AttemptTimeout — enforce it automatically.
    o.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(
      Math.Max(opts.AttemptTimeoutSeconds * 2 + 1, 30));
  }
}
