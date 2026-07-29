using FoodBridge.Domain.Enums;

namespace FoodBridge.Application.Abstractions;

/// <summary>
/// Repository projection for the recipient-side "available near me" geo query.
/// Mirrors <see cref="NearbyListing"/> — the volunteer equivalent — but also carries
/// the listing's current status and whether the caller already requested it, which is
/// what a browsing recipient needs to tell "Request" apart from "Requested", and a
/// still-Pending donation apart from one a volunteer is already collecting.
/// </summary>
public sealed class AvailableNearbyListing
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string FoodType { get; set; } = string.Empty;
    public DietType? DietType { get; set; }
    public MealType? MealType { get; set; }
    public int QuantityMeals { get; set; }
    public FreshnessTag FreshnessTag { get; set; }
    public DateTime PickupDeadlineUtc { get; set; }
    public string PickupAddress { get; set; } = string.Empty;
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public double DistanceMeters { get; set; }
    public ListingStatus Status { get; set; }
    public bool RequestedByMe { get; set; }
}
