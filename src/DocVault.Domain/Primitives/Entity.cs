namespace DocVault.Domain.Primitives;

public abstract class Entity<TId>
{
  public TId Id { get; protected set; }
  public DateTime CreatedAt { get; protected set; } = DateTime.UtcNow;
  public DateTime? UpdatedAt { get; protected set; }

  protected Entity(TId id)
  {
    Id = id;
  }

  protected void Touch() => UpdatedAt = DateTime.UtcNow;
}
