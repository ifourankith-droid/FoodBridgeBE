namespace FoodBridge.Domain.Entities;

/// <summary>
/// One completed drop-off. Append-only — rows are never updated, hence no UpdatedAtUtc.
/// Drives the post-delivery cooldown and hotspot intensity.
/// </summary>
public sealed class DropOffDelivery
{
    public Guid Id { get; set; }
    public Guid DropOffLocationId { get; set; }
    public Guid VolunteerId { get; set; }
    public Guid ListingId { get; set; }

    /// <summary>Meals delivered, copied from the listing so intensity doesn't need a join.</summary>
    public int MealsCount { get; set; }

    public DateTime DeliveredAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
