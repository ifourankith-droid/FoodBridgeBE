using System.Collections.Concurrent;
using FoodBridge.Application.Abstractions;

namespace FoodBridge.Infrastructure.Tracking;

public sealed class InMemoryTrackingStore : ITrackingStore
{
    private readonly ConcurrentDictionary<Guid, TrackedLocation> _locations = new();

    public void SetLocation(Guid listingId, decimal latitude, decimal longitude, DateTime reportedAtUtc)
    {
        _locations[listingId] = new TrackedLocation(latitude, longitude, reportedAtUtc);
    }

    public TrackedLocation? GetLocation(Guid listingId) =>
        _locations.TryGetValue(listingId, out var location) ? location : null;
}
