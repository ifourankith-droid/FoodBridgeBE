namespace FoodBridge.Application.Auth.Dtos;

/// <summary>
/// One complete postal address. Every part is nullable because an account may have been created
/// before a given field existed, or with location skipped entirely — the caller renders whatever is
/// present rather than assuming a shape.
/// </summary>
/// <param name="Label">
/// Short name for the place ("Home", "Main Branch"). Only ever set when the address came from a
/// donor's saved-addresses list, which is the only place a label is stored; null for the account's
/// own address on the Users row.
/// </param>
public sealed record AddressResponse(
    string? Label,
    string? Address,
    string? City,
    string? State,
    string? Pincode,
    decimal? Latitude,
    decimal? Longitude);
