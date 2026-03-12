using DocVault.Application.Abstractions.Messaging;
using DocVault.Domain.Events;
using Microsoft.Extensions.Logging;

namespace DocVault.Infrastructure.Messaging.Handlers;

/// <summary>
/// Handles the <see cref="DocumentImported"/> domain event.
/// Currently logs the import; extend this handler to trigger downstream workflows
/// (e.g. notifications, audit records).
/// </summary>
public sealed class DocumentImportedHandler : IEventHandler<DocumentImported>
{
  private readonly ILogger<DocumentImportedHandler> _logger;

  /// <summary>
  /// Initialises the handler with a logger.
  /// </summary>
  /// <param name="logger">Logger for structured log output.</param>
  public DocumentImportedHandler(ILogger<DocumentImportedHandler> logger)
  {
    _logger = logger;
  }

  /// <summary>
  /// Logs the document import event.
  /// </summary>
  /// <param name="event">The domain event carrying the imported document identifier.</param>
  /// <param name="cancellationToken">Cancellation token.</param>
  public Task HandleAsync(DocumentImported @event, CancellationToken cancellationToken = default)
  {
    _logger.LogInformation("Document {DocumentId} imported", @event.DocumentId);
    return Task.CompletedTask;
  }
}
