using FoodBridge.Application.DropOffLocations.Dtos;

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
    DateTime? EstimatedPickupAtUtc,
    /// <summary>
    /// When the donor confirmed the food is safe and its quality is their responsibility. Null only
    /// for listings created before that declaration was required.
    /// </summary>
    DateTime? FoodSafetyAcceptedAtUtc,
    string DonorName,
    string DonorMobile,
    string? VolunteerName,
    string? VolunteerMobile,
    string? RecipientName,
    string? RecipientMobile,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    IReadOnlyList<ListingImageResponse> Images,
    IReadOnlyList<ListingTimelineEntryResponse> Timeline,
    DropOffLocationResponse? SuggestedDropOffLocation = null);
