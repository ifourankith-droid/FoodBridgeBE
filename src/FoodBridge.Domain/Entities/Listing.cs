using FoodBridge.Domain.Enums;

namespace FoodBridge.Domain.Entities;

public sealed class Listing
{
    public Guid Id { get; set; }
    public Guid DonorId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string FoodType { get; set; } = string.Empty;
    public DietType? DietType { get; set; }
    public MealType? MealType { get; set; }
    public int QuantityMeals { get; set; }
    public FreshnessTag FreshnessTag { get; set; }
    public DateTime? PreparedAtUtc { get; set; }
    public DateTime PickupDeadlineUtc { get; set; }
    public string PickupAddress { get; set; } = string.Empty;
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public ListingStatus Status { get; set; }
    public Guid? VolunteerId { get; set; }
    public Guid? RecipientId { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
