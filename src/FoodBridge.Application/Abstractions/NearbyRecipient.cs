namespace FoodBridge.Application.Abstractions;

/// <summary>Repository projection for a donor's "nearby recipients" dashboard widget — informational browsing, not a match, so it isn't filtered by IsAvailable.</summary>
public sealed class NearbyRecipient
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? City { get; set; }
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public int? CapacityMeals { get; set; }
    public double DistanceMeters { get; set; }
}
