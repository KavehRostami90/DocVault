using DocVault.Application.Background;
using DocVault.Application.Pipeline;
using DocVault.Application.Pipeline.Stages;
using DocVault.Application.UseCases.Admin;
using DocVault.Application.UseCases.Documents.DeleteDocument;
using DocVault.Application.UseCases.Documents.GetDocument;
using DocVault.Application.UseCases.Documents.GetDocumentFile;
using DocVault.Application.UseCases.Documents.ImportDocument;
using DocVault.Application.UseCases.Documents.ListDocuments;
using DocVault.Application.UseCases.Documents.UpdateTags;
using DocVault.Application.UseCases.Imports.GetImportStatus;
using DocVault.Application.UseCases.Imports.StartImportJob;
using DocVault.Application.UseCases.Qa;
using DocVault.Application.UseCases.Search;
using DocVault.Application.UseCases.Tags.ListTags;
using Microsoft.Extensions.DependencyInjection;

namespace DocVault.Application;

public static class DependencyInjection
{
  public static IServiceCollection AddApplication(this IServiceCollection services)
  {
    services.AddScoped<ImportDocumentHandler>();
    services.AddScoped<GetDocumentHandler>();
    services.AddScoped<GetDocumentFileHandler>();
    services.AddScoped<ListDocumentsHandler>();
    services.AddScoped<UpdateTagsHandler>();
    services.AddScoped<DeleteDocumentHandler>();
    services.AddScoped<SearchDocumentsHandler>();
    services.AddScoped<AskQuestionHandler>();
    services.AddScoped<StartImportJobHandler>();
    services.AddScoped<GetImportStatusHandler>();
    services.AddScoped<ListTagsHandler>();
    services.AddScoped<GetAdminStatsHandler>();
    services.AddScoped<ReindexDocumentHandler>();
    services.AddScoped<ListUsersHandler>();

    services.AddSingleton<FileReadStage>();
    services.AddSingleton<TextExtractStage>();
    services.AddSingleton<ChunkingStage>();
    services.AddSingleton<EmbeddingStage>();
    services.AddSingleton<IndexStage>();
    services.AddSingleton<IIngestionPipeline, IngestionPipeline>();

    services.AddHostedService<IndexingWorker>();

    return services;
  }
}
