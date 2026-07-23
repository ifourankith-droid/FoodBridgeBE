namespace FoodBridge.Application.Auth.Dtos;

public sealed record UserResponse(
    Guid Id,
    string Mobile,
    string Name,
    string Role,
    string? City,
    string AccountStatus,
    string? RecipientType);
