namespace DocVault.Domain.Primitives;

public abstract class AggregateRoot<TId> : Entity<TId>
{
  private readonly List<object> _domainEvents = new();
  protected AggregateRoot(TId id) : base(id) { }

  public IReadOnlyCollection<object> DomainEvents => _domainEvents.AsReadOnly();

  protected void RaiseDomainEvent(object domainEvent) => _domainEvents.Add(domainEvent);
  public void ClearDomainEvents() => _domainEvents.Clear();
}
