namespace FoodBridge.Application.Listings.Dtos;

public sealed record ListingNearbyResponse(
    Guid Id,
    string Title,
    string FoodType,
    string? DietType,
    string? MealType,
    int QuantityMeals,
    string FreshnessTag,
    DateTime PickupDeadlineUtc,
    string PickupAddress,
    decimal Latitude,
    decimal Longitude,
    double DistanceKm);
