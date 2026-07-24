namespace FoodBridge.Application.Listings.Dtos;

public sealed record ListingTimelineEntryResponse(
    string? FromStatus,
    string ToStatus,
    Guid? ActorUserId,
    string? Note,
    string? PhotoUrl,
    DateTime CreatedAtUtc);
