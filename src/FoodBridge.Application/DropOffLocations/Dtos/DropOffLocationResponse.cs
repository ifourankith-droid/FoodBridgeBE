namespace FoodBridge.Application.DropOffLocations.Dtos;

/// <param name="Source">"Admin" for a curated partner site, "Volunteer" for one discovered in the field.</param>
public sealed record DropOffLocationResponse(
    Guid Id,
    string Name,
    string Address,
    decimal Latitude,
    decimal Longitude,
    string? City,
    bool IsActive,
    string Source,
    DateTime CreatedAtUtc);
