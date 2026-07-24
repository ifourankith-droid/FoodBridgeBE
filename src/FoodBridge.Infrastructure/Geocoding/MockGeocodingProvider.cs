using FoodBridge.Application.Abstractions;

namespace FoodBridge.Infrastructure.Geocoding;

/// <summary>
/// Deterministic stand-in for a real provider (Google Maps, Mapbox, ...) — matches a
/// handful of known Ahmedabad localities (the same ones used in seed data) by substring,
/// falling back to the city center marked IsApproximate for anything unrecognized.
/// </summary>
public sealed class MockGeocodingProvider : IGeocodingProvider
{
    private static readonly (string Locality, decimal Latitude, decimal Longitude)[] KnownLocalities =
    {
        ("Navrangpura", 23.0366m, 72.5607m),
        ("C.G. Road", 23.0338m, 72.5623m),
        ("Bodakdev", 23.0282m, 72.5061m),
        ("Satellite", 23.0209m, 72.5296m),
        ("Vastrapur", 23.0388m, 72.5292m),
        ("Maninagar", 22.9965m, 72.6032m),
        ("Paldi", 23.0089m, 72.5601m),
        ("Chandkheda", 23.1071m, 72.5832m),
    };

    private static readonly (decimal Latitude, decimal Longitude) AhmedabadCenter = (23.0225m, 72.5714m);

    public Task<GeocodeResult> GeocodeAsync(string address, CancellationToken cancellationToken = default)
    {
        foreach (var (locality, latitude, longitude) in KnownLocalities)
        {
            if (address.Contains(locality, StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(new GeocodeResult(latitude, longitude, IsApproximate: false));
            }
        }

        return Task.FromResult(new GeocodeResult(AhmedabadCenter.Latitude, AhmedabadCenter.Longitude, IsApproximate: true));
    }
}
