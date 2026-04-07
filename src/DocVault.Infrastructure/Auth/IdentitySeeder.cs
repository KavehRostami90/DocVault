using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DocVault.Infrastructure.Auth;

public sealed class IdentitySeeder
{
  private readonly RoleManager<IdentityRole> _roles;
  private readonly UserManager<ApplicationUser> _users;
  private readonly AuthSettings _settings;
  private readonly ILogger<IdentitySeeder> _logger;

  public IdentitySeeder(
    RoleManager<IdentityRole> roles,
    UserManager<ApplicationUser> users,
    IOptions<AuthSettings> settings,
    ILogger<IdentitySeeder> logger)
  {
    _roles = roles;
    _users = users;
    _settings = settings.Value;
    _logger = logger;
  }

  public async Task SeedAsync(CancellationToken ct = default)
  {
    await EnsureRoleAsync(AppRoles.Admin, ct);
    await EnsureRoleAsync(AppRoles.User, ct);
    await EnsureRoleAsync(AppRoles.Guest, ct);

    if (!string.IsNullOrWhiteSpace(_settings.AdminEmail) &&
        !string.IsNullOrWhiteSpace(_settings.AdminPassword))
    {
      await EnsureAdminUserAsync(ct);
    }
  }

  private async Task EnsureRoleAsync(string role, CancellationToken ct)
  {
    if (!await _roles.RoleExistsAsync(role))
    {
      var result = await _roles.CreateAsync(new IdentityRole(role));
      if (result.Succeeded)
        _logger.LogInformation("Created role {Role}", role);
      else
        _logger.LogWarning("Failed to create role {Role}: {Errors}", role, string.Join(", ", result.Errors.Select(e => e.Description)));
    }
  }

  private async Task EnsureAdminUserAsync(CancellationToken ct)
  {
    var existing = await _users.FindByEmailAsync(_settings.AdminEmail);
    if (existing is not null)
    {
      if (!await _users.IsInRoleAsync(existing, AppRoles.Admin))
        await _users.AddToRoleAsync(existing, AppRoles.Admin);
      return;
    }

    var admin = new ApplicationUser
    {
      UserName = _settings.AdminEmail,
      Email = _settings.AdminEmail,
      DisplayName = "Admin",
      EmailConfirmed = true,
    };

    var createResult = await _users.CreateAsync(admin, _settings.AdminPassword);
    if (!createResult.Succeeded)
    {
      _logger.LogWarning("Failed to create admin user: {Errors}",
        string.Join(", ", createResult.Errors.Select(e => e.Description)));
      return;
    }

    await _users.AddToRoleAsync(admin, AppRoles.Admin);
    _logger.LogInformation("Admin user {Email} created and seeded", _settings.AdminEmail);
  }
}
