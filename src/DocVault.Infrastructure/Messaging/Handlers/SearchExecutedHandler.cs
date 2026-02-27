using DocVault.Application.Abstractions.Messaging;
using DocVault.Domain.Events;
using Microsoft.Extensions.Logging;

namespace DocVault.Infrastructure.Messaging.Handlers;

public sealed class SearchExecutedHandler : IEventHandler<SearchExecuted>
{
  private readonly ILogger<SearchExecutedHandler> _logger;

  public SearchExecutedHandler(ILogger<SearchExecutedHandler> logger)
  {
    _logger = logger;
  }

  public Task HandleAsync(SearchExecuted @event, CancellationToken cancellationToken = default)
  {
    _logger.LogInformation("Search executed for query '{Query}'", @event.Query);
    return Task.CompletedTask;
  }
}
