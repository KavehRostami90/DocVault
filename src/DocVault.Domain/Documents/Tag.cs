using DocVault.Domain.Primitives;

namespace DocVault.Domain.Documents;

public class Tag : Entity<Guid>
{
  public string Name { get; private set; }

  private Tag() : base(Guid.Empty)
  {
    Name = string.Empty;
  }

  public Tag(Guid id, string name) : base(id)
  {
    Name = name;
  }
}
