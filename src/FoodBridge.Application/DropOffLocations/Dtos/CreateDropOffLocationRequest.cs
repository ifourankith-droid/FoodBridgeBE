namespace FoodBridge.Application.DropOffLocations.Dtos;

public sealed record CreateDropOffLocationRequest(
    string Name,
    string Address,
    decimal Latitude,
    decimal Longitude,
    string? City);
