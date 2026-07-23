namespace FoodBridge.Application.Abstractions;

/// <summary>
/// Reads the current request's JWT claims. Controllers and services depend on
/// this instead of touching <c>HttpContext.User</c> directly.
/// </summary>
public interface ICurrentUser
{
    Guid UserId { get; }
    string Role { get; }
    string Mobile { get; }
    string TokenId { get; }
    DateTime TokenExpiresAtUtc { get; }
    bool IsInRole(string role);
}
