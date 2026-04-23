namespace DocVault.Application.Abstractions.Cqrs;

/// <summary>
/// Marker interface for query inputs.
/// </summary>
public interface IQuery<TResult> { }

/// <summary>
/// Handles a query and returns a result. Implement this interface on all read-side handlers
/// so cross-cutting concerns (caching, tracing) can be applied uniformly via decorators.
/// </summary>
public interface IQueryHandler<TQuery, TResult> where TQuery : IQuery<TResult>
{
  Task<TResult> HandleAsync(TQuery query, CancellationToken cancellationToken = default);
}
