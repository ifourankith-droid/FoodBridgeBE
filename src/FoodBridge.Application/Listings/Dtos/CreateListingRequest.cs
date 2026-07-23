namespace FoodBridge.Application.Listings.Dtos;

public sealed record CreateListingRequest(
    string Title,
    string FoodType,
    string? DietType,
    string? MealType,
    int QuantityMeals,
    string FreshnessTag,
    DateTime? PreparedAtUtc,
    DateTime PickupDeadlineUtc,
    string PickupAddress,
    decimal Latitude,
    decimal Longitude);
