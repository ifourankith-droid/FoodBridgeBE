namespace FoodBridge.Application.DonorAddresses.Dtos;

public sealed record UpdateDonorAddressRequest(
    string Label,
    string Address,
    decimal Latitude,
    decimal Longitude,
    bool IsDefault);
