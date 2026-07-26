namespace FoodBridge.Domain.Entities;

public sealed class DonorAddress
{
    public Guid Id { get; set; }
    public Guid DonorId { get; set; }
    public string Label { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public bool IsDefault { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
