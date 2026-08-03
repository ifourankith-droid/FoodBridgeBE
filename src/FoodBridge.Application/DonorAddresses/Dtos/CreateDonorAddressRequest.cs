namespace FoodBridge.Application.DonorAddresses.Dtos;

/// <param name="City">Optional. Independent of the account's city — a branch can be elsewhere.</param>
/// <param name="Pincode">Optional, display-only: distance always comes from the coordinates.</param>
public sealed record CreateDonorAddressRequest(
    string Label,
    string Address,
    decimal Latitude,
    decimal Longitude,
    bool IsDefault,
    string? City = null,
    string? State = null,
    string? Pincode = null);
