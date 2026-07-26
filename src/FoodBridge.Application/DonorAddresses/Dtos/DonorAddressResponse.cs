namespace FoodBridge.Application.DonorAddresses.Dtos;

public sealed record DonorAddressResponse(
    Guid Id,
    string Label,
    string Address,
    decimal Latitude,
    decimal Longitude,
    bool IsDefault,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);
