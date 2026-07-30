namespace FoodBridge.Application.Common;

public sealed class DropOffSettings
{
    public const string SectionName = "DropOff";

    /// <summary>
    /// How long a drop-off spot is hidden from volunteers after receiving food. Applies to
    /// <em>every</em> volunteer, not just the one who delivered: the point is that the place
    /// itself has just been served, and a second volunteer arriving shortly after has no way
    /// of knowing that on their own. Also the window the nearest-spot suggestion skips.
    /// </summary>
    public int CooldownHours { get; set; } = 5;

    /// <summary>Default radius for the volunteer hotspot map, in km.</summary>
    public double HotspotRadiusKm { get; set; } = 10;

    /// <summary>Ceiling for a caller-supplied hotspot radius, in km.</summary>
    public double MaxHotspotRadiusKm { get; set; } = 50;
}
