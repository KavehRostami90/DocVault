using DocVault.Application.Background;
using DocVault.Application.Background.Queue;
using DocVault.Application.Pipeline;
using DocVault.Application.Pipeline.Hooks;
using DocVault.Application.Pipeline.Stages;
using DocVault.Application.UseCases.Documents.DeleteDocument;
using DocVault.Application.UseCases.Documents.GetDocument;
using DocVault.Application.UseCases.Documents.ImportDocument;
using DocVault.Application.UseCases.Documents.ListDocuments;
using DocVault.Application.UseCases.Documents.UpdateTags;
using DocVault.Application.UseCases.Imports.GetImportStatus;
using DocVault.Application.UseCases.Imports.StartImportJob;
using DocVault.Application.UseCases.Search;
using Microsoft.Extensions.DependencyInjection;

namespace DocVault.Application;

public static class DependencyInjection
{
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

    services.AddSingleton<FileReadStage>();
    services.AddSingleton<TextExtractStage>();
    services.AddSingleton<EmbeddingStage>();
    services.AddSingleton<IndexStage>();
    services.AddSingleton(_ => DefaultHooks.Empty);
    services.AddSingleton<IngestionPipeline>();
    services.AddSingleton(typeof(IWorkQueue<(string Path, string ContentType)>), typeof(InMemoryWorkQueue<(string Path, string ContentType)>));
    services.AddSingleton<IndexingWorker>();

    return services;
  }
}
