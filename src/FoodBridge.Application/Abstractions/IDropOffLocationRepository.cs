using FoodBridge.Domain.Entities;

namespace FoodBridge.Application.Abstractions;

public interface IDropOffLocationRepository
{
    Task<Guid> CreateAsync(DropOffLocation location, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<DropOffLocation> Items, int TotalCount)> GetAllAsync(int page, int pageSize, CancellationToken cancellationToken = default);

    Task<DropOffLocation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default);

    /// <summary>
    /// Nearest active location to the given point that is <em>not</em> cooling down — i.e. has
    /// had no delivery since <paramref name="cooldownSinceUtc"/> — or null if none qualify.
    /// This is what gets suggested to a volunteer, so a spot that was just served is never
    /// offered as the next destination.
    /// </summary>
    Task<DropOffLocation?> GetNearestAvailableAsync(decimal latitude, decimal longitude, DateTime cooldownSinceUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Active locations within <paramref name="radiusMeters"/>, each with its all-time delivery
    /// count, total meals, last-delivered timestamp and cooldown state. Ordered available-first,
    /// then nearest — so the volunteer's best next destination is the first row, while spots on
    /// cooldown still appear (labelled) rather than vanishing from the map.
    /// </summary>
    /// <param name="cooldown">
    /// Length of the post-delivery cooldown. Passed as a duration rather than just a cutoff
    /// because each row also reports *when* it becomes available again
    /// (<c>LastDeliveredAtUtc + cooldown</c>), which a bare cutoff can't express. The policy
    /// itself (how many hours) stays in configuration, not here.
    /// </param>
    Task<(IReadOnlyList<DropOffHotspot> Items, int TotalCount)> GetHotspotsAsync(
        decimal latitude,
        decimal longitude,
        double radiusMeters,
        DateTime nowUtc,
        TimeSpan cooldown,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}
