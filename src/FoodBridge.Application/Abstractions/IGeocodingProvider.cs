namespace FoodBridge.Application.Abstractions;

public sealed record GeocodeResult(decimal Latitude, decimal Longitude, bool IsApproximate);

/// <summary>
/// Resolves a free-form address to coordinates. Swap the mock for a real provider
/// (Google Maps, Mapbox, ...) by implementing this interface — no consumer changes.
/// </summary>
public interface IGeocodingProvider
{
    Task<GeocodeResult> GeocodeAsync(string address, CancellationToken cancellationToken = default);
}
