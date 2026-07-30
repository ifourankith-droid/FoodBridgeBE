using FoodBridge.Domain.Entities;

namespace FoodBridge.Application.Abstractions;

/// <summary>
/// A drop-off spot as a volunteer sees it: the location plus how heavily it's used, how far
/// away it is, and whether it's currently cooling down after a recent delivery.
/// </summary>
public sealed class DropOffHotspot
{
    public DropOffLocation Location { get; init; } = new();

    /// <summary>Distance from the volunteer's supplied position, in km.</summary>
    public double DistanceKm { get; init; }

    /// <summary>All-time deliveries recorded here — the hotspot's intensity.</summary>
    public int DeliveryCount { get; init; }

    /// <summary>Total meals delivered here across all time.</summary>
    public int TotalMeals { get; init; }

    /// <summary>When food last arrived, or null if it never has.</summary>
    public DateTime? LastDeliveredAtUtc { get; init; }

    /// <summary>
    /// When this spot becomes available again, or null when it isn't cooling down. Returned
    /// rather than a bare bool so the UI can show "available in 2h" instead of just "unavailable".
    /// </summary>
    public DateTime? CooldownUntilUtc { get; init; }

    public bool IsCoolingDown => CooldownUntilUtc.HasValue;
}
