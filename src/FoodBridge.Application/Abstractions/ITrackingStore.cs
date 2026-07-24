namespace FoodBridge.Application.Abstractions;

public sealed record TrackedLocation(decimal Latitude, decimal Longitude, DateTime ReportedAtUtc);

/// <summary>
/// Last-known volunteer location per in-flight listing. In-memory by design — this is
/// live, ephemeral state (not an audit trail; that's ListingTimeline), lost on restart
/// and not shared across instances. Acceptable for a single-instance deployment; a
/// multi-instance one would back this with a distributed cache (e.g. Redis) instead.
/// </summary>
public interface ITrackingStore
{
    void SetLocation(Guid listingId, decimal latitude, decimal longitude, DateTime reportedAtUtc);

    TrackedLocation? GetLocation(Guid listingId);
}
