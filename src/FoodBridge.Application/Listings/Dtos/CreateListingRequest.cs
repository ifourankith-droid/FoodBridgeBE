namespace FoodBridge.Application.Listings.Dtos;

/// <summary>
/// Either <paramref name="DonorAddressId"/> (a saved address from the caller's own address
/// book) or all three of <paramref name="PickupAddress"/>/<paramref name="Latitude"/>/
/// <paramref name="Longitude"/> must be provided — never both, never neither.
/// </summary>
/// <param name="AcceptedFoodSafety">
/// The donor's confirmation that the food is safe to eat and that its quality remains their
/// responsibility. **Must be true** — a create is rejected otherwise.
/// <para>
/// Required on the API, not merely presented as a checkbox in the UI: the point of the declaration
/// is that it was actually given for this specific donation, and a client-side-only tick would be
/// trivially bypassed by calling this endpoint directly. Omitting the field deserialises to
/// <c>false</c>, so a caller written against the old contract is refused rather than silently
/// treated as having agreed.
/// </para>
/// </param>
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
    decimal? Longitude,
    bool AcceptedFoodSafety);
