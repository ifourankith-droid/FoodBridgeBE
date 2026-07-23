using FoodBridge.Application.Auth.Dtos;
using FoodBridge.Domain.Entities;

namespace FoodBridge.Application.Auth;

public static class UserMapper
{
    public static UserResponse ToResponse(this User user) => new(
        user.Id,
        user.Mobile,
        user.Name,
        user.Role.ToString(),
        user.City,
        user.AccountStatus.ToString(),
        user.RecipientType?.ToString());
}
