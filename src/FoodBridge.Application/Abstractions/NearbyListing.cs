using FoodBridge.Domain.Enums;

namespace FoodBridge.Application.Abstractions;

/// <summary>Repository projection for the nearby-listings geo query — carries the computed distance alongside the fields a browsing volunteer needs.</summary>
public sealed class NearbyListing
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
}
