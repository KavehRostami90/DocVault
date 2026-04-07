using System.Security.Claims;
using DocVault.Application.Abstractions.Auth;
using DocVault.Infrastructure.Auth;

namespace DocVault.Api.Services;

public sealed class CurrentUserService : ICurrentUser
{
  private readonly IHttpContextAccessor _http;

  public CurrentUserService(IHttpContextAccessor http)
  {
    _http = http;
  }

  private ClaimsPrincipal? User => _http.HttpContext?.User;

  public Guid? UserId
  {
    get
    {
      var sub = User?.FindFirstValue(ClaimTypes.NameIdentifier)
             ?? User?.FindFirstValue("sub");
      return Guid.TryParse(sub, out var id) ? id : null;
    }
  }

  public string? Email => User?.FindFirstValue(ClaimTypes.Email)
                       ?? User?.FindFirstValue("email");

  public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

  public bool IsAdmin => User?.IsInRole(AppRoles.Admin) ?? false;

  public bool IsGuest => string.Equals(
    User?.FindFirstValue("isGuest"), "true", StringComparison.OrdinalIgnoreCase);
}
