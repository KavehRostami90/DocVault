namespace DocVault.Domain.Documents.ValueObjects;

public readonly record struct RelevanceScore(double Value)
{
  public static RelevanceScore Zero => new(0);
  public override string ToString() => Value.ToString("F3");
}
