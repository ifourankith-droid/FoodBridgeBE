using FoodBridge.Application.DropOffLocations.Dtos;
using FoodBridge.Domain.Entities;

namespace FoodBridge.Application.DropOffLocations;

public static class DropOffLocationMapper
{
    public static DropOffLocationResponse ToResponse(this DropOffLocation location) => new(
        location.Id,
        location.Name,
        location.Address,
        location.Latitude,
        location.Longitude,
        location.City,
        location.IsActive,
        location.CreatedAtUtc);
}
