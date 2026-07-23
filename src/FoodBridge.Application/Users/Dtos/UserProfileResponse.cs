namespace FoodBridge.Application.Users.Dtos;

public sealed record UserProfileResponse(
    Guid Id,
    string Mobile,
    string Name,
    string Role,
    string? City,
    string? Address,
    decimal? Latitude,
    decimal? Longitude,
    string? RecipientType,
    int? CapacityMeals,
    bool IsAvailable,
    string AccountStatus,
    string? AvatarUrl);
