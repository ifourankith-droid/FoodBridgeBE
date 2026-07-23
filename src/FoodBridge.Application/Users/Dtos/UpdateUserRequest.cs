namespace FoodBridge.Application.Users.Dtos;

/// <summary>
/// <paramref name="CapacityMeals"/> only applies to Recipients and is ignored otherwise.
/// Role and RecipientType cannot be changed after registration.
/// </summary>
public sealed record UpdateUserRequest(
    string Name,
    string? City,
    string? Address,
    decimal? Latitude,
    decimal? Longitude,
    int? CapacityMeals);
