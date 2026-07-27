namespace FoodBridge.Application.Dashboard.Dtos;

public sealed record NearbyRecipientResponse(
    Guid Id,
    string Name,
    string? Address,
    string? City,
    decimal Latitude,
    decimal Longitude,
    int? CapacityMeals,
    double DistanceKm);
