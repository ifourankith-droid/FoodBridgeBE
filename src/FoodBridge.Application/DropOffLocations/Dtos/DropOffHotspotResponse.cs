namespace FoodBridge.Application.DropOffLocations.Dtos;

/// <summary>
/// A drop-off spot as shown to a volunteer on the hotspot map: where it is, how heavily it's
/// used, and whether it's currently on cooldown after a recent delivery.
/// </summary>
/// <param name="Source">"Admin" for a curated partner site, "Volunteer" for a field-discovered spot.</param>
/// <param name="DeliveryCount">All-time deliveries here — the hotspot's intensity.</param>
/// <param name="TotalMeals">All-time meals delivered here.</param>
/// <param name="IsCoolingDown">
/// True when this spot was served recently and should not receive another delivery yet. It is still
/// returned (rather than filtered out) so the map stays complete and the volunteer can see *why*
/// a nearby spot isn't being suggested.
/// </param>
/// <param name="CooldownUntilUtc">When it becomes available again; null when it already is.</param>
public sealed record DropOffHotspotResponse(
    Guid Id,
    string Name,
    string Address,
    decimal Latitude,
    decimal Longitude,
    string? City,
    string Source,
    double DistanceKm,
    int DeliveryCount,
    int TotalMeals,
    DateTime? LastDeliveredAtUtc,
    bool IsCoolingDown,
    DateTime? CooldownUntilUtc);
