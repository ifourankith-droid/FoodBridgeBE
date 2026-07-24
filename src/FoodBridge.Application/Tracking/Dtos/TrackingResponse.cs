namespace FoodBridge.Application.Tracking.Dtos;

public sealed record TrackingResponse(Guid ListingId, decimal Latitude, decimal Longitude, DateTime ReportedAtUtc);
