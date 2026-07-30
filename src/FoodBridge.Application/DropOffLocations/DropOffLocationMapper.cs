using FoodBridge.Application.Abstractions;
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
        location.Source.ToString(),
        location.CreatedAtUtc);

    public static DropOffHotspotResponse ToResponse(this DropOffHotspot hotspot) => new(
        hotspot.Location.Id,
        hotspot.Location.Name,
        hotspot.Location.Address,
        hotspot.Location.Latitude,
        hotspot.Location.Longitude,
        hotspot.Location.City,
        hotspot.Location.Source.ToString(),
        // One decimal place is all a volunteer needs, and it keeps the payload from carrying
        // meaningless float precision straight out of STDistance.
        Math.Round(hotspot.DistanceKm, 1),
        hotspot.DeliveryCount,
        hotspot.TotalMeals,
        hotspot.LastDeliveredAtUtc,
        hotspot.IsCoolingDown,
        hotspot.CooldownUntilUtc);
}
