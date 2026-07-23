namespace FoodBridge.Infrastructure.Common;

/// <summary>
/// Shared SQL fragment for building a SQL Server geography point from
/// <c>@Latitude</c>/<c>@Longitude</c> parameters, so repositories don't repeat
/// the literal expression everywhere they read or write a Location column.
/// </summary>
public static class GeoHelper
{
    public const string PointFromLatLngFragment = "geography::Point(@Latitude, @Longitude, 4326)";
}
