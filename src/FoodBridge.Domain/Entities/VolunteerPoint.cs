namespace FoodBridge.Domain.Entities;

public sealed class VolunteerPoint
{
    public Guid Id { get; set; }
    public Guid VolunteerId { get; set; }
    public Guid ListingId { get; set; }
    public int Points { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
