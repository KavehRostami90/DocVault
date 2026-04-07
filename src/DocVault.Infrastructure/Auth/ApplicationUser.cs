using Microsoft.AspNetCore.Identity;

namespace DocVault.Infrastructure.Auth;

public sealed class ApplicationUser : IdentityUser
{
  public string DisplayName { get; set; } = string.Empty;
  public bool IsGuest { get; set; }
  public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
