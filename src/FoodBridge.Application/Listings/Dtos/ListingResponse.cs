namespace FoodBridge.Application.Listings.Dtos;

public sealed record ListingResponse(
    Guid Id,
    Guid DonorId,
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
    decimal Longitude,
    string Status,
    Guid? VolunteerId,
    Guid? RecipientId,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    IReadOnlyList<ListingImageResponse> Images,
    IReadOnlyList<ListingTimelineEntryResponse> Timeline);
