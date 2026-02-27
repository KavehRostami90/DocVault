using DocVault.Application.Abstractions.Messaging;
using DocVault.Domain.Events;
using Microsoft.Extensions.Logging;

namespace DocVault.Infrastructure.Messaging.Handlers;

public sealed class DocumentImportedHandler : IEventHandler<DocumentImported>
{
  private readonly ILogger<DocumentImportedHandler> _logger;

  public DocumentImportedHandler(ILogger<DocumentImportedHandler> logger)
  {
    _logger = logger;
  }

  public Task HandleAsync(DocumentImported @event, CancellationToken cancellationToken = default)
  {
    _logger.LogInformation("Document {DocumentId} imported", @event.DocumentId);
    return Task.CompletedTask;
  }
}
