using FoodBridge.Domain.Enums;

namespace FoodBridge.Domain.Entities;

public sealed class ListingTimelineEvent
{
    public Guid Id { get; set; }
    public Guid ListingId { get; set; }
    public ListingStatus? FromStatus { get; set; }
    public ListingStatus ToStatus { get; set; }
    /// <summary>Null for system-initiated events (e.g. automatic expiry) — no human acted.</summary>
    public Guid? ActorUserId { get; set; }
    public string? Note { get; set; }
    public string? PhotoUrl { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
