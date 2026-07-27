namespace FoodBridge.Application.DropOffLocations.Dtos;

public sealed record DropOffLocationResponse(
    Guid Id,
    string Name,
    string Address,
    decimal Latitude,
    decimal Longitude,
    string? City,
    bool IsActive,
    DateTime CreatedAtUtc);
