using FoodBridge.Domain.Enums;

namespace FoodBridge.Domain.Entities;

public sealed class User
{
    public Guid Id { get; set; }
    public string Mobile { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public string? City { get; set; }
    public string? Address { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public RecipientType? RecipientType { get; set; }
    public int? CapacityMeals { get; set; }
    public bool IsAvailable { get; set; }
    public AccountStatus AccountStatus { get; set; }
    public string? AvatarUrl { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
