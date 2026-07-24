using FoodBridge.Application.Common;
using FoodBridge.Application.Geocoding.Dtos;

namespace FoodBridge.Application.Geocoding;

public interface IGeocodingService
{
    Task<Result<GeocodeResponse>> GeocodeAsync(string address, CancellationToken cancellationToken = default);
}
