using FoodBridge.Application.DonorAddresses.Dtos;
using FoodBridge.Domain.Entities;

namespace FoodBridge.Application.DonorAddresses;

public static class DonorAddressMapper
{
    public static DonorAddressResponse ToResponse(this DonorAddress address) => new(
        address.Id,
        address.Label,
        address.Address,
        address.Latitude,
        address.Longitude,
        address.IsDefault,
        address.CreatedAtUtc,
        address.UpdatedAtUtc);
}
