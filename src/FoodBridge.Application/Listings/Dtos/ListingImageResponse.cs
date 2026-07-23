namespace FoodBridge.Application.Listings.Dtos;

public sealed record ListingImageResponse(Guid Id, string ImageUrl, DateTime CreatedAtUtc);
