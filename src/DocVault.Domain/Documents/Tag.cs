using DocVault.Domain.Common;
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
    SetName(name);
  }

  public Tag(string name) : base(Guid.NewGuid())
  {
    SetName(name);
  }

  private void SetName(string name)
  {
    if (string.IsNullOrWhiteSpace(name))
      throw new DomainException("Tag name cannot be empty or whitespace");

    var trimmedName = name.Trim();

    if (trimmedName.Length < ValidationConstants.Tags.MIN_NAME_LENGTH || trimmedName.Length > ValidationConstants.Tags.MAX_NAME_LENGTH)
      throw new DomainException($"Tag name must be between {ValidationConstants.Tags.MIN_NAME_LENGTH} and {ValidationConstants.Tags.MAX_NAME_LENGTH} characters");

    if (!trimmedName.All(c => char.IsLetterOrDigit(c) || c == '-' || c == '_'))
      throw new DomainException("Tag name can only contain letters, digits, hyphens, and underscores");

    Name = trimmedName.ToLowerInvariant();
  }
}
