namespace FoodBridge.Application.Listings.Dtos;

/// <summary>Only permitted while the listing is Pending.</summary>
public sealed record UpdateListingRequest(
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
