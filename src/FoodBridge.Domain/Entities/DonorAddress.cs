namespace FoodBridge.Domain.Entities;

public sealed class DonorAddress
{
    public Guid Id { get; set; }
    public Guid DonorId { get; set; }
    public string Label { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string? City { get; set; }
    public string? State { get; set; }
    /// <summary>Postal code, display-only — <c>Location</c> remains the authority for distance.</summary>
    public string? Pincode { get; set; }
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public bool IsDefault { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
