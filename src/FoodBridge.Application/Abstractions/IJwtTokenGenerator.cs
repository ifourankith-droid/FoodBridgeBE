using FoodBridge.Domain.Entities;

namespace FoodBridge.Application.Abstractions;

public interface IJwtTokenGenerator
{
    string GenerateToken(User user);
}
