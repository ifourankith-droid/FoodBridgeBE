using FoodBridge.Domain.Enums;

namespace FoodBridge.Application.Abstractions;

/// <summary>Repository projection for the admin accounts browse.</summary>
public sealed class AdminUserSummary
{
    public Guid Id { get; set; }
    public string Mobile { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public AccountStatus AccountStatus { get; set; }
    public string? City { get; set; }
    public bool IsAvailable { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
