using DocVault.Application.Abstractions.Messaging;
using DocVault.Domain.Events;
using Microsoft.Extensions.Logging;

namespace DocVault.Infrastructure.Messaging.Handlers;

/// <summary>
/// Handles the <see cref="SearchExecuted"/> domain event.
/// Currently logs the executed query; extend this handler to collect query analytics
/// or trigger cache warm-up.
/// </summary>
public sealed class SearchExecutedHandler : IEventHandler<SearchExecuted>
{
  private readonly ILogger<SearchExecutedHandler> _logger;

  /// <summary>
  /// Initialises the handler with a logger.
  /// </summary>
  /// <param name="logger">Logger for structured log output.</param>
  public SearchExecutedHandler(ILogger<SearchExecutedHandler> logger)
  {
    _logger = logger;
  }

  /// <summary>
  /// Logs the search-executed event.
  /// </summary>
  /// <param name="event">The domain event carrying the executed query string.</param>
  /// <param name="cancellationToken">Cancellation token.</param>
  public Task HandleAsync(SearchExecuted @event, CancellationToken cancellationToken = default)
  {
    _logger.LogInformation("Search executed for query '{Query}'", @event.Query);
    return Task.CompletedTask;
  }
}
