using DocVault.Api.Composition;
using DocVault.Api.Endpoints;
using DocVault.Api.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDocVault(builder.Configuration);
builder.Services.AddApiOptions(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();

if (app.Environment.IsDevelopment())
{
  app.MapOpenApi();
}

app.MapDocumentsEndpoints();
app.MapSearchEndpoints();
app.MapTagsEndpoints();
app.MapImportsEndpoints();
app.MapHealthEndpoints();

app.Run();
