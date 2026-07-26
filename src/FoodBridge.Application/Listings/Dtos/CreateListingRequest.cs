namespace FoodBridge.Application.Listings.Dtos;

/// <summary>
/// Either <paramref name="DonorAddressId"/> (a saved address from the caller's own address
/// book) or all three of <paramref name="PickupAddress"/>/<paramref name="Latitude"/>/
/// <paramref name="Longitude"/> must be provided — never both, never neither.
/// </summary>
public sealed record CreateListingRequest(
    string Title,
    string FoodType,
    string? DietType,
    string? MealType,
    int QuantityMeals,
    string FreshnessTag,
    DateTime? PreparedAtUtc,
    DateTime PickupDeadlineUtc,
    Guid? DonorAddressId,
    string? PickupAddress,
    decimal? Latitude,
    decimal? Longitude);
