using FoodBridge.Application.Abstractions;
using FoodBridge.Application.Common;
using FoodBridge.Application.Geocoding.Dtos;

namespace FoodBridge.Application.Geocoding;

public sealed class GeocodingService : IGeocodingService
{
    private readonly IGeocodingProvider _geocodingProvider;

    public GeocodingService(IGeocodingProvider geocodingProvider)
    {
        _geocodingProvider = geocodingProvider;
    }

    public async Task<Result<GeocodeResponse>> GeocodeAsync(string address, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return Result.Failure<GeocodeResponse>("An address is required.");
        }

        var result = await _geocodingProvider.GeocodeAsync(address, cancellationToken);
        return Result.Success(new GeocodeResponse(result.Latitude, result.Longitude, result.IsApproximate));
    }
}
