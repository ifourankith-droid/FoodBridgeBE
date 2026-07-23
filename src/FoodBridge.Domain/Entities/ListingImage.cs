namespace FoodBridge.Domain.Entities;

public sealed class ListingImage
{
    public Guid Id { get; set; }
    public Guid ListingId { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
