namespace FoodBridge.Application.Auth.Dtos;

public sealed record AuthResponse(string Token, UserResponse User);
