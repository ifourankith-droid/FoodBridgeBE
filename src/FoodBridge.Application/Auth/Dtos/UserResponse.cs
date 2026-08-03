namespace FoodBridge.Application.Auth.Dtos;

/// <param name="City">
/// Kept at the top level as well as inside <paramref name="Address"/>: it predates the address block
/// and callers already read it for the identity header. It always mirrors the Users row, so it can
/// differ from <c>Address.City</c> when a donor's default saved address is in another city.
/// </param>
/// <param name="Address">
/// The complete address, or null when the account has none. For donors this is their **default saved
/// address** (so it carries a label and matches what a new donation would be posted from), falling
/// back to the account's own address when they have no saved addresses.
/// </param>
public sealed record UserResponse(
    Guid Id,
    string Mobile,
    string Name,
    string Role,
    string? City,
    string AccountStatus,
    string? RecipientType,
    string? AvatarUrl,
    AddressResponse? Address = null);
