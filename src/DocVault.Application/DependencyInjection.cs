using DocVault.Application.Background;
using DocVault.Application.Background.Queue;
using DocVault.Application.Pipeline;
using DocVault.Application.Pipeline.Hooks;
using DocVault.Application.Pipeline.Stages;
using DocVault.Application.UseCases.Admin;
using DocVault.Application.UseCases.Documents.DeleteDocument;
using DocVault.Application.UseCases.Documents.GetDocument;
using DocVault.Application.UseCases.Documents.ImportDocument;
using DocVault.Application.UseCases.Documents.ListDocuments;
using DocVault.Application.UseCases.Documents.UpdateTags;
using DocVault.Application.UseCases.Imports.GetImportStatus;
using DocVault.Application.UseCases.Imports.StartImportJob;
using DocVault.Application.UseCases.Search;
using DocVault.Application.UseCases.Tags.ListTags;
using Microsoft.Extensions.DependencyInjection;

namespace DocVault.Application;

public static class DependencyInjection
{
  /// <summary>
  /// Registers application-layer services, handlers, pipeline stages, and background workers.
  /// </summary>
  /// <param name="services">Service collection to populate.</param>
  public static IServiceCollection AddApplication(this IServiceCollection services)
  {
    services.AddScoped<ImportDocumentHandler>();
    services.AddScoped<GetDocumentHandler>();
    services.AddScoped<ListDocumentsHandler>();
    services.AddScoped<UpdateTagsHandler>();
    services.AddScoped<DeleteDocumentHandler>();
    services.AddScoped<SearchDocumentsHandler>();
    services.AddScoped<StartImportJobHandler>();
    services.AddScoped<GetImportStatusHandler>();
    services.AddScoped<ListTagsHandler>();
    services.AddScoped<GetAdminStatsHandler>();
    services.AddScoped<ReindexDocumentHandler>();

    services.AddSingleton<FileReadStage>();
    services.AddSingleton<TextExtractStage>();
    services.AddSingleton<EmbeddingStage>();
    services.AddSingleton<IndexStage>();
    services.AddSingleton(_ => DefaultHooks.Empty);
    services.AddSingleton<IIngestionPipeline, IngestionPipeline>();

    // Channel-backed queue: thread-safe, async-blocking, no extra package.
    services.AddSingleton<IWorkQueue<IndexingWorkItem>, ChannelWorkQueue<IndexingWorkItem>>();

    // Register as a hosted service so the runtime starts/stops it automatically.
    services.AddHostedService<IndexingWorker>();

    return services;
  }
}

