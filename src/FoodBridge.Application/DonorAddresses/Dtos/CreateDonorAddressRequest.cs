namespace FoodBridge.Application.DonorAddresses.Dtos;

public sealed record CreateDonorAddressRequest(
    string Label,
    string Address,
    decimal Latitude,
    decimal Longitude,
    bool IsDefault);
