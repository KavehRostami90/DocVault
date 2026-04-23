namespace DocVault.Application.Abstractions.Cqrs;

/// <summary>
/// Marker interface for command inputs.
/// </summary>
public interface ICommand<TResult> { }

/// <summary>
/// Handles a command and produces a result. Implement this interface on all write-side handlers
/// so cross-cutting concerns (logging, validation, retry) can be applied uniformly via decorators.
/// </summary>
public interface ICommandHandler<TCommand, TResult> where TCommand : ICommand<TResult>
{
  Task<TResult> HandleAsync(TCommand command, CancellationToken cancellationToken = default);
}
