namespace FoodBridge.Application.Geocoding.Dtos;

public sealed record GeocodeResponse(decimal Latitude, decimal Longitude, bool IsApproximate);
