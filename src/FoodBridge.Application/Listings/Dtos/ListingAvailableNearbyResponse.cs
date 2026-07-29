namespace FoodBridge.Application.Listings.Dtos;

/// <summary>
/// One donation in a recipient's "available near me" browse feed. Same shape as
/// <see cref="ListingNearbyResponse"/> plus <c>Status</c> (Pending = not yet collected,
/// Claimed = a volunteer is on the way) and <c>IsRequestedByMe</c>.
/// </summary>
public sealed record ListingAvailableNearbyResponse(
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
    double DistanceKm,
    string Status,
    bool IsRequestedByMe);
