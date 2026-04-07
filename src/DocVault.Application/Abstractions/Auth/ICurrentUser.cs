namespace DocVault.Application.Abstractions.Auth;

public interface ICurrentUser
{
  Guid? UserId { get; }
  string? Email { get; }
  bool IsAuthenticated { get; }
  bool IsAdmin { get; }
  bool IsGuest { get; }
}
