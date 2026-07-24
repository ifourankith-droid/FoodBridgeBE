namespace FoodBridge.Application.Admin.Dtos;

public sealed record AdminUserSummaryResponse(
    Guid Id,
    string Mobile,
    string Name,
    string Role,
    string AccountStatus,
    string? City,
    bool IsAvailable,
    DateTime CreatedAtUtc);
