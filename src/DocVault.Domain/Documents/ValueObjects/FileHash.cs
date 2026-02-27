namespace DocVault.Domain.Documents.ValueObjects;

public readonly record struct FileHash(string Value)
{
  public static FileHash FromBytes(byte[] bytes) => new(Convert.ToHexString(bytes));
  public override string ToString() => Value;
}
