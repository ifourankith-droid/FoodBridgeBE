namespace FoodBridge.Application.DonorAddresses.Dtos;

/// <inheritdoc cref="CreateDonorAddressRequest"/>
public sealed record UpdateDonorAddressRequest(
    string Label,
    string Address,
    decimal Latitude,
    decimal Longitude,
    bool IsDefault,
    string? City = null,
    string? State = null,
    string? Pincode = null);
