using FoodBridge.Application.Users.Dtos;
using FoodBridge.Domain.Entities;

namespace FoodBridge.Application.Users;

public static class UserProfileMapper
{
    public static UserProfileResponse ToProfileResponse(this User user) => new(
        user.Id,
        user.Mobile,
        user.Name,
        user.Role.ToString(),
        user.City,
        user.Address,
        user.Latitude,
        user.Longitude,
        user.RecipientType?.ToString(),
        user.CapacityMeals,
        user.IsAvailable,
        user.AccountStatus.ToString(),
        user.AvatarUrl);
}
