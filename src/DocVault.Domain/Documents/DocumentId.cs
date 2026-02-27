namespace DocVault.Domain.Documents;

public readonly record struct DocumentId(Guid Value)
{
  public static DocumentId New() => new(Guid.NewGuid());
  public override string ToString() => Value.ToString();
}
