namespace FoodBridge.Application.Listings.Dtos;

/// <summary>
/// One lifecycle event for the standalone item-timeline endpoint: the status it moved
/// to, who did it (name resolved from the actor), when, and any note / proof photo.
/// Shared by the donor and volunteer sections.
/// </summary>
public sealed record ListingTimelineEventResponse(
    string? FromStatus,
    string Status,
    Guid? ActorUserId,
    string? ActorName,
    string? Note,
    string? PhotoUrl,
    DateTime CreatedAtUtc);
