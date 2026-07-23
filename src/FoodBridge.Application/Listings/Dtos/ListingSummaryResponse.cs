namespace FoodBridge.Application.Listings.Dtos;

/// <summary>Lightweight shape for list views — no images/timeline.</summary>
public sealed record ListingSummaryResponse(
    Guid Id,
    string Title,
    string FoodType,
    string? DietType,
    string? MealType,
    int QuantityMeals,
    string FreshnessTag,
    DateTime PickupDeadlineUtc,
    string Status,
    DateTime CreatedAtUtc);
