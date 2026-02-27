using DocVault.Domain.Documents;
using DocVault.Domain.Imports;

namespace DocVault.Domain.Extensions;

/// <summary>
/// Domain-focused extension members grouped for reuse across layers.
/// </summary>
public static class Extensions
{
  public static bool IsIndexed(this Document document) => document.Status == DocumentStatus.Indexed;
  public static bool IsPending(this Document document) => document.Status == DocumentStatus.Pending;
  public static bool IsFailed(this Document document) => document.Status == DocumentStatus.Failed;
  public static bool HasTag(this Document document, string tag)
    => document.Tags.Any(t => t.Name.Equals(tag, StringComparison.OrdinalIgnoreCase));

  public static bool IsTerminal(this ImportJob job) => job.Status is ImportStatus.Completed or ImportStatus.Failed;
}
